﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Design;
using System.Data.Entity.Migrations.Infrastructure;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EFAutomation.Exceptions;
using Microsoft.CSharp;

namespace EFAutomation
{
    /// <summary>
    /// 
    /// </summary>
    public class AutoContextFactory : IAutoContextFactory
    {
   
        private readonly List<Type> _entitiesToBaseOn;
        private readonly List<Type> _entities;
        private readonly List<Assembly> _assemblies;
        private bool _migrated;

        /// <summary>
        /// Static instance
        /// </summary>
        public static AutoContextFactory Current { get; protected set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public AutoContextFactory()
        {
            Configuration = new AutoContextFactoryConfiguration()
            {
                AutoGeneratedMigrationsEnabled = false,
                AutoMigrateToLatestVersionEnabled = false,
                AutomaticMigrationDataLossAllowed = false,
                MigrationsDirectory = "AutomaticMigrations",
                Connection = "DefaultConnection",
                AutoMigrateGeneratedMigrationsEnabled = false,
                MigrationsAssemblyAsFile = false,
                MigrationsAssemblyFileLocation = "AutomaticMigrations\\Migrations.dll"
            };
            _entitiesToBaseOn = new List<Type>();
            _entities = new List<Type>();
            _assemblies = new List<Assembly>();
            Current = this;
        }

        public IAutoContextFactoryConfiguration Configuration { get; set; }

        #region Add entities and assemblies

        /// <summary>
        /// Lists all types 
        /// </summary>
        /// <returns></returns>
        public List<Type> EntitiesToBaseOn()
        {
            return _entitiesToBaseOn;
        }

        /// <summary>
        /// Lists all single types that are to be included in the context.
        /// </summary>
        /// <returns>List of types</returns>
        public List<Type> Entities()
        {
            return _entities;
        }

        /// <summary>
        /// Lists all assemblies that are searched for entities that are based on a Base Entity.
        /// </summary>
        /// <returns></returns>
        public List<Assembly> AssembliesThatContain()
        {
            return _assemblies;
        }

        /// <summary>
        /// Adds all objects that are extending from this base class that are not abstract to the context
        /// </summary>
        /// <typeparam name="T">Class which' inheritors are to be added to the context</typeparam>
        /// <returns>AutoContextFactory for fluent chaining</returns>
        public IAutoContextFactory AddEntitiesBasedOn<T>() where T : class
        {
            _entitiesToBaseOn.Add(typeof (T));
            return this;
        }

        /// <summary>
        /// Adds a single object to context
        /// </summary>
        /// <typeparam name="T">Class to add</typeparam>
        /// <returns>AutoContextFactory for fluent chaining</returns>
        public IAutoContextFactory AddEntity<T>() where T : class
        {
            _entities.Add(typeof (T));
            return this;
        }

        /// <summary>
        /// Adds assembly which is to be used to search inherited classes from.
        /// </summary>
        /// <typeparam name="T">Class that is contained within the required assembly</typeparam>
        /// <returns>AutoContextFactory for fluent chaining</returns>
        public IAutoContextFactory AddAssemblyContaining<T>() where T : class
        {
            _assemblies.Add(Assembly.GetAssembly(typeof (T)));
            return this;
        }

        /// <summary>
        /// Adds assembly to be used to search inherited classes from.
        /// </summary>
        /// <param name="assembly">Assembly to add</param>
        /// <returns>AutoContextFactory for fluent chaining</returns>
        public IAutoContextFactory AddAssembly(Assembly assembly)
        {
            _assemblies.Add(assembly);
            return this;
        }

        /// <summary>
        /// Gets all included types in the context
        /// </summary>
        /// <returns>List of all types in the context</returns>
        public List<Type> IncludedTypes()
        {
            var types = new List<Type>();
            foreach (var assembly in _assemblies)
            {
                foreach (var entity in _entitiesToBaseOn)
                {
                    var entity1 = entity;
                    types.AddRange(assembly.GetTypes().Where(x => x.IsSubclassOf(entity1) && !x.IsAbstract));
                }
            }
            types.AddRange(_entities);
            return types.Distinct().ToList();
        }

        #endregion

        /// <summary>
        /// Creates the context
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns>Created IContext</returns>
        public IContext Context(string connectionString = "")
        {
            if(!_migrated)
                MigrateToLatest();
            return new Context(_assemblies, _entities, _entitiesToBaseOn,
                connectionString != "" ? connectionString : Configuration.Connection);
        }

        private MigrationConfiguration CreateConfiguration()
        {
            var configuration = new MigrationConfiguration();
            configuration.Seeding += (sender, context) => Seeding(sender, context);
            configuration.AutomaticMigrationDataLossAllowed = Configuration.AutomaticMigrationDataLossAllowed;
            configuration.AutomaticMigrationsEnabled = Configuration.AutoMigrateToLatestVersionEnabled;
            configuration.MigrationsNamespace = "EFMigrations";
            if (Configuration.MigrationsAssemblyAsFile)
            {
                if (!File.Exists(Configuration.MigrationsAssemblyFileLocation))
                    CompileMigrationsAssembly(true);
                try
                {
                    var bytes = File.ReadAllBytes(Configuration.MigrationsAssemblyFileLocation.Trim());
                    configuration.MigrationsAssembly = Assembly.Load(bytes);
                }
                catch (FileNotFoundException ex)
                {
                    throw new FileLoadException(
                        "Compiled migrations assembly file was not found or was not able to be compiled.", ex);
                }
            }
            else
                configuration.MigrationsAssembly = CompileMigrationsAssembly(false);
            return configuration;
        }

        /// <summary>
        /// Migrates database to latest version
        /// </summary>
        public void MigrateToLatest()
        {
            try
            {
                TryMigrate();
            }
            catch (Exception ex)
            {
                // We can handle these exceptions here. Rest is likely user error.
                if (!(ex is AutomaticMigrationsDisabledException) && !(ex is DirectoryNotFoundException)) throw;
                if (Configuration.AutoGeneratedMigrationsEnabled)
                {
                    GenerateMigrations();
                    TryMigrate();
                }
                else
                    throw new MigrationsOutOfDateException(
                        "Migrations are out of date but AutoGeneratedMigrationsEnabled is false", ex);
            }

        }

        private void TryMigrate()
        {
            var configuration = CreateConfiguration();
            // Migrate
            var migrator = new DbMigrator(configuration);
            var pending = migrator.GetPendingMigrations();
            migrator.Update();
            _migrated = true;
        }

        /// <summary>
        /// Generates freshest migrations
        /// </summary>
        public void GenerateMigrations()
        {
            var conf = CreateConfiguration();
            
            var migrationScaffolder = new MigrationScaffolder(conf);
            var name = "AutoGeneratedMigration" + DateTime.Now.ToString("yyMMddhhmmss");
            var scaffoldedMigration = migrationScaffolder.Scaffold(name);
            if (!Directory.Exists(Configuration.MigrationsDirectory))
                Directory.CreateDirectory(Configuration.MigrationsDirectory);
            File.WriteAllText(Path.Combine(Configuration.MigrationsDirectory, scaffoldedMigration.MigrationId + ".cs"), scaffoldedMigration.UserCode);
            var designerGenerator = new MigrationDesignerGenerator
            {
                Session =
                    new Dictionary<string, object>
                    {
                        {"Target", scaffoldedMigration.Resources["Target"]},
                        {"MigrationId", scaffoldedMigration.MigrationId},
                        {"ClassName", name}
                    }
            };
            designerGenerator.Initialize();
            File.WriteAllText(
                Path.Combine(Configuration.MigrationsDirectory, scaffoldedMigration.MigrationId + ".Designer.cs"),
                designerGenerator.TransformText());

            if (Configuration.MigrationsAssemblyAsFile)
                CompileMigrationsAssembly(true);
        }

        private Assembly CompileMigrationsAssembly(bool asFile)
        {
            if (asFile && Configuration.MigrationsAssemblyFileLocation.Trim().Length == 0)
                throw new DirectoryNotFoundException("Configuration indicates migration should be compiled into a file but no file name was specified");
            // Compile migrations assembly
            var parameters = DefaultCompilerParameters();

            // Compiler locks the destination file so we have to use a temp dir and copy it later
            var tempDir = Path.GetTempFileName();
            if (asFile)
            {
                parameters.GenerateInMemory = false;
                parameters.OutputAssembly = tempDir;
            }
            var codeProvider = new CSharpCodeProvider();
            var files = Directory.GetFiles(Configuration.MigrationsDirectory).Where(x => x.EndsWith(".cs")).Select(File.ReadAllText);
            var compiled = codeProvider.CompileAssemblyFromSource(parameters, files.ToArray());

            if (compiled.Errors.Count > 0)
                throw new FileLoadException("Assembly file is currently in use.");

            if (asFile)
                File.Copy(tempDir, Configuration.MigrationsAssemblyFileLocation, true);

            return compiled.CompiledAssembly;
        }

        private CompilerParameters DefaultCompilerParameters()
        {
            var compilerParams = new CompilerParameters();
            compilerParams.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(DbContext)).Location);
            compilerParams.ReferencedAssemblies.Add("System.Data.dll");
            compilerParams.ReferencedAssemblies.Add("System.Data.Entity.dll");
            compilerParams.ReferencedAssemblies.Add("System.Core.dll");
            compilerParams.ReferencedAssemblies.Add("System.dll");
            compilerParams.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(IContext)).Location);
            compilerParams.GenerateInMemory = false;
            return compilerParams;
        }

        /// <summary>
        /// When model is created. Only used for Context.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        internal void OnModelCreating(object sender, ModelBuilderEventArgs args)
        {
            if (ModelCreating != null)
                ModelCreating(sender, args);
        }

        public event SeedingEventHandler Seeding;
        public event ModelCreatingEventHandler ModelCreating;
    }

}
