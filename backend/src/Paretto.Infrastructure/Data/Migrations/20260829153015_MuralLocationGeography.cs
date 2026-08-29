using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Paretto.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MuralLocationGeography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Columna nueva, nullable por ahora: todavía tenemos que volcar los valores existentes
            //    de Latitude/Longitude antes de poder exigir NOT NULL (FR-06/AC-04).
            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "Murals",
                type: "geography",
                nullable: true);

            // 2. Backfill (AC-04): geography::Point(Latitude, Longitude, 4326) toma
            //    (latitud, longitud, SRID) en ese orden — coherente con Mural.CreateLocation, que
            //    construye el Point en C# con el mismo eje. Si alguna fila existente tiene
            //    coordenadas fuera de rango WGS84 (p. ej. latitud > 90), geography::Point lanza una
            //    excepción de SQL Server acá mismo y todo el Up() se revierte (transacción por
            //    defecto de EF Core, no desactivada — R3 del threat model): falla explícita, no
            //    silenciosa (AC-05).
            migrationBuilder.Sql("UPDATE Murals SET Location = geography::Point(Latitude, Longitude, 4326);");

            // 3. Ya con todas las filas pobladas, se exige NOT NULL.
            migrationBuilder.AlterColumn<Point>(
                name: "Location",
                table: "Murals",
                type: "geography",
                nullable: false,
                oldClrType: typeof(Point),
                oldType: "geography",
                oldNullable: true);

            // 4. Índice espacial: primer uso de SQL crudo en una migración de este proyecto porque el
            //    Fluent API de EF Core no tiene soporte nativo para CREATE SPATIAL INDEX.
            migrationBuilder.Sql(
                "CREATE SPATIAL INDEX SPATIAL_IX_Murals_Location ON Murals(Location) USING GEOGRAPHY_AUTO_GRID;");

            // 5-7. El índice B-tree y las columnas que reemplaza dejan de existir (FR-07/AC-06).
            migrationBuilder.DropIndex(
                name: "IX_Murals_Status_Latitude_Longitude",
                table: "Murals");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Murals");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Murals");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Orden inverso y simétrico de Up(): recrear Latitude/Longitude nullable, backfill
            // inverso, NOT NULL, recrear el índice B-tree viejo, borrar el índice espacial y
            // finalmente la columna Location.
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Murals",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Murals",
                type: "float",
                nullable: true);

            // Backfill inverso: `geography.Lat`/`geography.Long` son las propiedades nativas de SQL
            // Server para leer de vuelta las mismas coordenadas con las que se construyó el Point.
            migrationBuilder.Sql("UPDATE Murals SET Latitude = Location.Lat, Longitude = Location.Long;");

            migrationBuilder.AlterColumn<double>(
                name: "Latitude",
                table: "Murals",
                type: "float",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "Longitude",
                table: "Murals",
                type: "float",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Murals_Status_Latitude_Longitude",
                table: "Murals",
                columns: new[] { "Status", "Latitude", "Longitude" });

            migrationBuilder.Sql("DROP INDEX SPATIAL_IX_Murals_Location ON Murals;");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Murals");
        }
    }
}
