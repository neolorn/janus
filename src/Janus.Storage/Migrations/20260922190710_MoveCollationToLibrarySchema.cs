using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class MoveCollationToLibrarySchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The column depends on the collation it carries, so the one inside the
        // library's schema is created, the column is moved onto it, and only then
        // is the one outside dropped.
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:CollationDefinition:janus.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False")
            .Annotation("Npgsql:CollationDefinition:public.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False")
            .OldAnnotation("Npgsql:CollationDefinition:public.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False");

        // Written out because the provider quotes a column's collation as one
        // identifier, and this one is named by its schema.
        migrationBuilder.Sql(
            "ALTER TABLE janus.organizations "
            + "ALTER COLUMN name TYPE text COLLATE janus.janus_ci;");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:CollationDefinition:janus.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False")
            .OldAnnotation("Npgsql:CollationDefinition:janus.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False")
            .OldAnnotation("Npgsql:CollationDefinition:public.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:CollationDefinition:public.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False")
            .Annotation("Npgsql:CollationDefinition:janus.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False")
            .OldAnnotation("Npgsql:CollationDefinition:janus.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False");

        migrationBuilder.Sql(
            "ALTER TABLE janus.organizations "
            + "ALTER COLUMN name TYPE text COLLATE public.janus_ci;");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:CollationDefinition:public.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False")
            .OldAnnotation("Npgsql:CollationDefinition:public.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False")
            .OldAnnotation("Npgsql:CollationDefinition:janus.janus_ci", "und-u-ks-level2,und-u-ks-level2,icu,False");
    }
}
