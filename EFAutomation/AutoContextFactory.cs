﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Design;
using System.Data.Entity.Migrations.Infrastructure;
using System.Data.Odbc;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Hosting;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using EFAutomation.Exceptions;
using Microsoft.CSharp;

namespace EFAutomation
{
    public class AutoContextFactory : IAutoContextFactory
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool UnlockFile(IntPtr handle, int offsetLow, int offsetHi);

        private int _migrationAttempts;

        public IAutoContextFactoryConfiguration Configuration { get; set; }

        private readonly List<Assembly> _assemblies = new List<Assembly>();
        private readonly List<Type> _subClassesOf = new List<Type>();
        private readonly List<Type> _singleClasses = new List<Type>();
        private readonly GeneratedContext _contextGenerator;
        private IContext _context;
        private IDbMigrationsConfiguration _dbMigrationsConfiguration;
        private CSharpCodeProvider _codeProvider;
        private bool _migrated = false;
        public event SeedingEventHandler Seeding;
        private CompilerResults _compilerResults;
        //private ProxyDomain _proxyDomain;
        # region Add assemblies

        public IAutoContextFactory AddEntitiesBasedOn<T>() where T : class
        {
            _subClassesOf.Add(typeof (T));
            return this;
        }

        public IAutoContextFactory AddEntity<T>() where T : class
        {
            _singleClasses.Add(typeof(T));
            return this;
        }

        public IAutoContextFactory AddAssemblyContaining<T>() where T : class
        {
            _assemblies.Add(Assembly.GetAssembly(typeof (T)));
            return this;
        }

        public IAutoContextFactory AddAssembly(Assembly assembly)
        {
            _assemblies.Add(assembly);
            return this;
        }

        #endregion

        public AutoContextFactory()
        {
            _contextGenerator = new GeneratedContext();
            _codeProvider = new CSharpCodeProvider();
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
        }

        public AutoContextFactory(IAutoContextFactoryConfiguration configuration) : this()
        {
            Configuration = configuration;
        }

        private void CompileAssembliesAndCreateClasses()
        {
            var compilerParameters = DefaultCompilerParameters();
            var types = new List<Type>();
            foreach (var assembly in _assemblies)
            {
                foreach (var entity in _subClassesOf)
                {
                    var entity1 = entity;
                    types.AddRange(assembly.GetTypes().Where(x => x.IsSubclassOf(entity1) && !x.IsAbstract));
                }
            }
            types.AddRange(_singleClasses);
            types = types.Distinct().ToList();
            compilerParameters.ReferencedAssemblies.AddRange(
                types.Select(x => Assembly.GetAssembly(x).Location).Distinct().ToArray());
            var namespaces = new List<string>();
            namespaces.AddRange(types.Select(x => x.Namespace));

            // Set variables for context generator
            _contextGenerator.Session = new Dictionary<string, object>();
            _contextGenerator.Session["Types"] = types;
            _contextGenerator.Session["Assemblies"] = namespaces;
            if (Configuration.AutoMigrateToLatestVersionEnabled)
                _contextGenerator.Session["MigrateToLatestVersion"] = true;
            _contextGenerator.Session["Connection"] = Configuration.Connection;
            _contextGenerator.Initialize();

            var generatedContextSource = _contextGenerator.TransformText();
            _compilerResults = _codeProvider.CompileAssemblyFromSource(compilerParameters,
                generatedContextSource);
            if (_compilerResults.Errors.Count > 0)
                throw new AssemblyCompilationErrorsException(_compilerResults.Errors);

            CreateMigrationsConfiguration();
            _context = (IContext)_compilerResults.CompiledAssembly.CreateInstance("EFMigrations.Context", false, BindingFlags.CreateInstance, null,
                        new object[] { "DefaultConnection" }, CultureInfo.CurrentCulture, null);
        }

        private void CreateMigrationsConfiguration()
        {
            _dbMigrationsConfiguration =
                (IDbMigrationsConfiguration)
                    _compilerResults.CompiledAssembly.CreateInstance("EFMigrations.Configuration");
            _dbMigrationsConfiguration.Seeding += DbMigrationsConfigurationOnSeeding;
        }

        private void DbMigrationsConfigurationOnSeeding(object sender, SeedingEventArgs args)
        {
            if (Seeding != null)
                Seeding(sender, args);
        }

        public IContext Context()
        {
            if(_context == null)
                CompileAssembliesAndCreateClasses();

            if(Configuration.AutoMigrateGeneratedMigrationsEnabled && !_migrated)
                MigrateToLatest();

            return _context;
        }

        public void MigrateToLatest()
        {
            if (_migrationAttempts > 2)
            {
                _migrationAttempts = 0;
                return;
            }

            try
            {
                TryMigrate();
                _migrationAttempts = 0;
            }
            catch (Exception ex)
            {
                if (!(ex is AutomaticMigrationsDisabledException) && !(ex is DirectoryNotFoundException)) throw;
                _migrationAttempts++;
                if (Configuration.AutoGeneratedMigrationsEnabled)
                {
                    GenerateMigrations();
                    if (Configuration.MigrationsAssemblyAsFile)
                        CompileMigrationsAssembly(true);
                    MigrateToLatest();
                }
                else
                    throw new MigrationsOutOfDateException(
                        "Migrations are out of date but AutoGeneratedMigrationsEnabled is false", ex);
            }
        }

        public void GenerateMigrations()
        {
            if(_dbMigrationsConfiguration == null)
                CompileAssembliesAndCreateClasses();

            var migrationScaffolder = new MigrationScaffolder((DbMigrationsConfiguration)_dbMigrationsConfiguration) { Namespace = "EFMigrations" };
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
            if(asFile && Configuration.MigrationsAssemblyFileLocation.Trim().Length == 0)
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
            _codeProvider = new CSharpCodeProvider();
            var files = Directory.GetFiles(Configuration.MigrationsDirectory).Where(x => x.EndsWith(".cs")).Select(File.ReadAllText);
            var compiled = _codeProvider.CompileAssemblyFromSource(parameters, files.ToArray());
            
            if(compiled.Errors.Count > 0)
                throw new FileLoadException("Assembly file is currently in use.");

            if(asFile)
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
        
        private void TryMigrate()
        {
            if (Configuration.MigrationsAssemblyAsFile)
            {
                if (Configuration.MigrationsAssemblyFileLocation.Trim().Length == 0)
                    throw new DirectoryNotFoundException(
                        "Configuration indicates migration should be compiled into a file but no file name was specified");
                if (!File.Exists(Configuration.MigrationsAssemblyFileLocation.Trim()))
                {
                    CompileMigrationsAssembly(true);
                }
                var bytes = File.ReadAllBytes(Configuration.MigrationsAssemblyFileLocation.Trim());
                _dbMigrationsConfiguration.MigrationsAssembly = Assembly.Load(bytes);
            }
            else
            {
                _dbMigrationsConfiguration.MigrationsAssembly = CompileMigrationsAssembly(false);
            }
            // Migrate
            var migrator = new DbMigrator((DbMigrationsConfiguration)_dbMigrationsConfiguration);
            migrator.Update();
            _migrated = true;
        }
        
    }
}
