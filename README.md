EFAutomation
============

EFAutomation is a convention based extension library for Entity Framework to automate several tasks currently cumbersome to do. It allows developers to create an Entity Framework based Context without having to specify each entity by putting it as DbSet<> as property. 

NuGet: http://www.nuget.org/packages/EFAutomation/

Basic usage
============

1) Create the context factory
```c#
private IAutoContextFactory _autoContextFactory;
_autoContextFactory = new AutoContextFactory();
```

2) Configure the factory
```c#
_autoContextFactory.Configuration.AutoGeneratedMigrationsEnabled = true; 
_autoContextFactory.Configuration.AutoMigrateGeneratedMigrationsEnabled = true;
_autoContextFactory.Configuration.MigrationsDirectory = @"ProjectDir\Migrations";
_autoContextFactory.AddEntitiesBasedOn<BaseEntity>().AddAssemblyContaining<BaseEntity>();
```
3) Create the context and use it
```c#
var context = _autoContextFactory.Context();
context.Set<Item>().Add(new Item());
context.SaveChanges();
```

Detailed info
=============

### Lifecycle
Factory lifecyle should be same as your applicatin lifecycle. Every Context() call checks if database has already been migrated, provided AutoMigrateGeneratedMigrationsEnabled is set to true. If you manage migrations manually, lifecycle can be anything and you will be responsible for performance and errors. IContext lifecycle can be anything you would normally set a standard DbContext to.

### Context
EFAutomation declares an IContext interface which declares effectively same functions as original DbContext and adds some of its own, such as events. You are free to cast it to standard DbContext if you wish.

Context() call from Factory also accepts a string parameter for connection string, either full connectionstring or the name of it, just like standard DbContext. It's suggested to just set the Connection option under Configuration property, though.

### Configuration
**IAutoContextFactoryConfiguration** provides several configuration options. 
* **MigrationsDirectory** - where you want the program to store its migration files. The files are standard Code First Migration files. This directory should be included in your source control to allow synchronized migrations between developers.

* **AutoMigrateToLatestVersionEnabled** - this is an option normally used in standard Code First Migration Configuration as _AutomaticMigrationsEnabled_, having this option on disables the Code Based migration generation.

* **AutomaticMigrationDataLossAllowed** - goes together with AutoMigrateToLatestVersionEnabled, allows data loss on automatic migrations.

* **AutoGeneratedMigrationsEnabled** - option used to specify that, if model has changed, a new migration should be generated. This option exists so programmatic migration generation through Factory could be used.

* **AutoMigrateGeneratedMigrationsEnabled** - option used to specify whether, if model has changed, newly generated migrations should be migrated to database. If this feature is disabled, migrations need to be done programmatically through Factory. If AutoGeneratedMigrationsEnabled is set to false, migrations need to be generated programmatically or migrations will fail.

* **Connection** - your standard connectionstring or connectionstring name

* **MigrationsAssemblyAsFile** - option used if you want migrations to be compiled into a file or loaded from an existing file. Useful if you want to deploy your application but not distribute the .cs  files. Make sure to set the file name.

* **MigrationsAssemblyFileLocation** - name of the file the compiled migration assembly should be saved to. Only functions when MigrationsAssemblyAsFile is true.

**NB! Order of operations is important. Any configuration should be done before Context(), MigrateToLatest() or GenerateMigrations() are called for the first time on Factory, although before migrating, anything ought to work.**

### Factory
* **Configuration** - IAutoContextFactoryConfiguration for configuring the factory

* **AddEntitiesBasedOn&lt;T&gt;()** - This method allows convention based adding of entities to the context. All entities that are not abstract and are based on this class (or these classes, if you add more than one) are automatically added. Assemblies to be searched from also needs to be added.

* **AddEntity&lt;T&gt;()** - Adds a single entity to context.

* **AddAssemblyContaining&lt;T&gt;()** - Adds an assembly which should be searched for convention-added classes, which contains specified class.
```c#
public class EntityBase
{
  public int Id { get; set;
}

public class EntityOne : EntityBase {}
public class EntityTwo : EntityBase {}

factory.AddEntitiesBasedOn<EntityBase>().AddAssemblyContaining<EntityOne>(); 
// Adds EntityOne and EntityTwo to context
```

* **AddAssembly(Assembly assembly)** - Adds a single assembly that should be searched for convention-added classes.

* **Context()** - Retrieves the context (and also causes it to be generated if it hasn't yet). If AutoMigrateGeneratedMigrationsEnabled is true, migrations are also run.

* **MigrateToLatest()** - Migrates the database to latest version (and also causes context to be generated if it hasn't yet). If AutoGeneratedMigrationsEnabled is true, missing migrations are automatically generated.

* **GenerateMigrations()** - Generates migrations and saves them under specified migrations directory (and also causes context to be generated if it hasn't yet). _Does not automatically migrate_.

* **IncludedTypes()** - Returns a list of types currently included in the context.
* **AssembliesThatContain()** - Returns a list of assemblies that should be searched.
* **Entities()** - Returns a list of types that are single included in context.
* **EntitiesToBaseOn()** - Returns a list of base types to be used for searching.

**NB! Order of operations is important. Any other method should be called before Context(), MigratToLatest() or GenerateMigrations() are called for the first time on Factory** 

Events
==========
**All events fire before their original base events**

### IContext Events
* SavingChanges - executed when changes are being saved. Gets executed in both Async and normal saving.
```c#
context.SavingChanges += (sender, args) => { args.Context.(...);/* args.Context is IContext */ };
```

### IAutoContextFactory Events
* Seeding - executed when database is seeded. Put your AddOrUpdate events here.
```c#
factory.Seeding += (sender, args) => { args.Context.(...); /* args.Context is IContext */ };
```
* ModelCreating - executed when model is being created. Conventions should be set up here.
```c#
context.ModelCreating += (sender, args) => { args.ModelBuilder.(...);/* args.ModelBuilder is standard DbModelBuilder */};
```
Version history
==========

#### v 2.0
* Complete rework of migrations and context generation. Now using Reflection instead of T4, allowing the runtime injection of ModelCreating object which wasn't working in version 1.
* Added support for retrieving currently stored entities, base entities and assemblies for Context generation.

#### v 1.0.3
* Context() now accepts a connectionString parameter

#### v 1.0.2
* Context() now has its own lifecycle.

#### v 1.0.1
* Added IncludedTypes() to Factory

#### v 1.0
* Initial release.

TODO
==========
* Perhaps give separate events for async savechanges?
* Add Identity support.

Known bugs
==========
* Might have random file access errors.
