using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PartitionAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            
            migrationBuilder.Sql("ALTER TABLE \"AuditLogs\" RENAME TO \"AuditLogs_Old\";");
            migrationBuilder.Sql("ALTER TABLE \"AuditLogs_Old\" RENAME CONSTRAINT \"PK_AuditLogs\" TO \"PK_AuditLogs_Old\";");
            migrationBuilder.Sql("ALTER INDEX \"IX_AuditLogs_EnvironmentId_Timestamp\" RENAME TO \"IX_AuditLogs_Old_EnvironmentId_Timestamp\";");
            migrationBuilder.Sql("ALTER INDEX \"IX_AuditLogs_EntityName_EntityId\" RENAME TO \"IX_AuditLogs_Old_EntityName_EntityId\";");
            migrationBuilder.Sql("ALTER INDEX \"IX_AuditLogs_PerformedById\" RENAME TO \"IX_AuditLogs_Old_PerformedById\";");

        
            migrationBuilder.Sql(@"
                CREATE TABLE ""AuditLogs"" (
                    ""Id"" uuid NOT NULL,
                    ""EnvironmentId"" uuid,
                    ""EntityName"" character varying(128) NOT NULL,
                    ""EntityFriendlyName"" character varying(256) NOT NULL,
                    ""EntityId"" character varying(128) NOT NULL,
                    ""Action"" character varying(64) NOT NULL,
                    ""OldValues"" jsonb,
                    ""NewValues"" jsonb,
                    ""PerformedById"" uuid,
                    ""PerformedByEmail"" character varying(256) NOT NULL,
                    ""Timestamp"" timestamp with time zone NOT NULL,
                    ""ProjectId"" uuid,
                    CONSTRAINT ""PK_AuditLogs"" PRIMARY KEY (""Id"", ""Timestamp"")
                ) PARTITION BY RANGE (""Timestamp"");
            ");

            migrationBuilder.Sql(@"CREATE INDEX ""IX_AuditLogs_EnvironmentId_Timestamp"" ON ""AuditLogs"" (""EnvironmentId"", ""Timestamp"");");
            migrationBuilder.Sql(@"CREATE INDEX ""IX_AuditLogs_EntityName_EntityId"" ON ""AuditLogs"" (""EntityName"", ""EntityId"");");
            migrationBuilder.Sql(@"CREATE INDEX ""IX_AuditLogs_PerformedById"" ON ""AuditLogs"" (""PerformedById"");");

            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    start_date DATE := '2025-01-01';
                    end_date DATE;
                    partition_name TEXT;
                BEGIN
                    FOR i IN 0..36 LOOP
                        end_date := start_date + INTERVAL '1 month';
                        partition_name := 'AuditLogs_' || to_char(start_date, 'YYYY_MM');
                        EXECUTE format('CREATE TABLE IF NOT EXISTS %I PARTITION OF ""AuditLogs"" FOR VALUES FROM (%L) TO (%L);', partition_name, start_date, end_date);
                        start_date := end_date;
                    END LOOP;
                END $$;
            ");
            
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""AuditLogs_Archive"" PARTITION OF ""AuditLogs"" FOR VALUES FROM (MINVALUE) TO ('2025-01-01');");

            migrationBuilder.Sql(@"
                INSERT INTO ""AuditLogs"" 
                (""Id"", ""EnvironmentId"", ""EntityName"", ""EntityFriendlyName"", ""EntityId"", ""Action"", ""OldValues"", ""NewValues"", ""PerformedById"", ""PerformedByEmail"", ""Timestamp"", ""ProjectId"")
                SELECT 
                    ""Id"", 
                    ""EnvironmentId"", 
                    ""EntityName"", 
                    ""EntityFriendlyName"", 
                    ""EntityId"", 
                    ""Action"", 
                    CAST(""OldValues"" AS jsonb), 
                    CAST(""NewValues"" AS jsonb), 
                    ""PerformedById"", 
                    ""PerformedByEmail"", 
                    ""Timestamp"", 
                    ""ProjectId""
                FROM ""AuditLogs_Old"";
            ");

            migrationBuilder.Sql(@"DROP TABLE ""AuditLogs_Old"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"AuditLogs\" RENAME TO \"AuditLogs_Partitioned\";");

            migrationBuilder.Sql(@"
                CREATE TABLE ""AuditLogs"" (
                    ""Id"" uuid NOT NULL,
                    ""EnvironmentId"" uuid,
                    ""EntityName"" character varying(128) NOT NULL,
                    ""EntityFriendlyName"" character varying(256) NOT NULL,
                    ""EntityId"" character varying(128) NOT NULL,
                    ""Action"" character varying(64) NOT NULL,
                    ""OldValues"" text,
                    ""NewValues"" text,
                    ""PerformedById"" character varying(128),
                    ""PerformedByEmail"" character varying(256),
                    ""Timestamp"" timestamp with time zone NOT NULL,
                    ""ProjectId"" uuid NOT NULL,
                    CONSTRAINT ""PK_AuditLogs"" PRIMARY KEY (""Id"")
                );
            ");

            migrationBuilder.Sql(@"CREATE INDEX ""IX_AuditLogs_EnvironmentId_Timestamp"" ON ""AuditLogs"" (""EnvironmentId"", ""Timestamp"");");
            migrationBuilder.Sql(@"CREATE INDEX ""IX_AuditLogs_EntityName_EntityId"" ON ""AuditLogs"" (""EntityName"", ""EntityId"");");
            migrationBuilder.Sql(@"CREATE INDEX ""IX_AuditLogs_PerformedById"" ON ""AuditLogs"" (""PerformedById"");");

            migrationBuilder.Sql(@"
                INSERT INTO ""AuditLogs"" 
                (""Id"", ""EnvironmentId"", ""EntityName"", ""EntityFriendlyName"", ""EntityId"", ""Action"", ""OldValues"", ""NewValues"", ""PerformedById"", ""PerformedByEmail"", ""Timestamp"", ""ProjectId"")
                SELECT 
                    ""Id"", 
                    ""EnvironmentId"", 
                    ""EntityName"", 
                    ""EntityFriendlyName"", 
                    ""EntityId"", 
                    ""Action"", 
                    CAST(""OldValues"" AS text), 
                    CAST(""NewValues"" AS text), 
                    ""PerformedById"", 
                    ""PerformedByEmail"", 
                    ""Timestamp"", 
                    ""ProjectId""
                FROM ""AuditLogs_Partitioned"";
            ");

            migrationBuilder.Sql(@"DROP TABLE ""AuditLogs_Partitioned"";");
        }
    }
}
