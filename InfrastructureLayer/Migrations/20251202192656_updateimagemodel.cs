using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class updateimagemodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
          

            //migrationBuilder.AddColumn<string>(
            //    name: "CloudinaryPublicId",
            //    table: "Images",
            //    type: "longtext",
            //    nullable: true)
            //    .Annotation("MySql:CharSet", "utf8mb4");

           
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
           

           

            //migrationBuilder.DropColumn(
            //    name: "CloudinaryPublicId",
            //    table: "Images");

          
        }
    }
}
