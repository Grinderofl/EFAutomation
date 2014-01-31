namespace EFMigrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AutoGeneratedMigration140131092349 : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Items",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Created = c.DateTime(nullable: false),
                        Data = c.String(),
                        Data2 = c.String(),
                        Data3 = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.Items");
        }
    }
}
