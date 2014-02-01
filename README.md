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

### Context
EFAutomation declares an IContext interface which declares effectively same functions as original DbContext and adds some of its own, such as events. You are free to cast it to standard DbContext if you wish.

Context is **factory persistent**. Once created, it stays the way it is for the entire lifecycle of the factory. Any events added to the context are also present in every other Context() call. This is due a few sacrifices needed to make in order to give the factory a little bit of modularity.

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

**NB! Order of operations is important. Any configuration should be done before Context(), MigratToLatest() or GenerateMigrations() are called for the first time on Factory**

### Factory
* **Configuration** - IAutoContextFactoryConfiguration for configuring the factory

* **AddEntitiesBasedOn<T>()** - This method allows convention based adding of entities to the context. All entities that are not abstract and are based on this class (or these classes, if you add more than one) are automatically added. Assemblies to be searched from also needs to be added.

* **AddEntity<T>()** - Adds a single entity to context.

* **AddAssemblyContaining<T>()** - Adds an assembly which should be searched for convention-added classes, which contains specified class.

* **AddAssembly(Assembly assembly)** - Adds a single assembly that should be searched for convention-added classes.

* **Context()** - Retrieves the context (and also causes it to be generated if it hasn't yet). If AutoMigrateGeneratedMigrationsEnabled is true, migrations are also run.

* **MigrateToLatest()** - Migrates the database to latest version (and also causes context to be generated if it hasn't yet). If AutoGeneratedMigrationsEnabled is true, missing migrations are automatically generated.

* **GenerateMigrations()** - Generates migrations and saves them under specified migrations directory (and also causes context to be generated if it hasn't yet). _Does not automatically migrate_.

**NB! Order of operations is important. Any other method should be called before Context(), MigratToLatest() or GenerateMigrations() are called for the first time on Factory** 

Events
==========
**All events fire before their original base events**

### IContext Events
* SavingChanges - executed when changes are being saved. Gets executed in both Async and normal saving.
```c#
context.SavingChanges += (sender, args) => { args.Context.(...);/* args.Context is IContext */ };
```

* ModelCreating - executed when model is being created. Conventions should be set up here.
```c#
context.ModelCreating += (sender, args) => { args.ModelBuilder.(...);/* args.ModelBuilder is standard DbModelBuilder */};
```

### IAutoContextFactory Events
* Seeding - executed when database is seeded. Put your AddOrUpdate events here.
```c#
factory.Seeding += (sender, args) => { args.Context.(...); /* args.Context is IContext */ };
```


TODO
==========
* Perhaps give separate events for async savechanges?

KNOW BUGS
==========
* Might have random file access errors.
