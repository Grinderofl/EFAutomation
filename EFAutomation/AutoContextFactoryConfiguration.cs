﻿namespace EFConvention
{
    /// <summary>
    /// Auto Context Factory Configuration class
    /// </summary>
    public class AutoContextFactoryConfiguration : IAutoContextFactoryConfiguration
    {
        public string MigrationsDirectory { get; set; }
        public bool AutomaticMigrationsEnabled { get; set; }
        public bool AutoGeneratedMigrationsEnabled { get; set; }
        public bool AutomaticMigrationDataLossAllowed { get; set; }
        public string Connection { get; set; }
        public bool AutoMigrateGeneratedMigrationsEnabled { get; set; }
        public bool MigrationsAssemblyAsFile { get; set; }
        public string MigrationsAssemblyFileLocation { get; set; }
    }
}
