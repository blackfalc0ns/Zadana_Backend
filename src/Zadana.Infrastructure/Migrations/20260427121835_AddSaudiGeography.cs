using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaudiGeography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SaudiRegions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    MapZoom = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaudiRegions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaudiCities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    MapZoom = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaudiCities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaudiCities_SaudiRegions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "SaudiRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaudiCities_Code",
                table: "SaudiCities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaudiCities_RegionId",
                table: "SaudiCities",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_SaudiRegions_Code",
                table: "SaudiRegions",
                column: "Code",
                unique: true);

            SeedSaudiGeography(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaudiCities");

            migrationBuilder.DropTable(
                name: "SaudiRegions");
        }

        private static void SeedSaudiGeography(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc]) VALUES
('10000000-0000-0000-0000-000000000001', N'RIYADH', N'منطقة الرياض', N'Riyadh Region', 24.7136, 46.6753, 8, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('10000000-0000-0000-0000-000000000002', N'MAKKAH', N'منطقة مكة المكرمة', N'Makkah Region', 21.4225, 39.8262, 8, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('10000000-0000-0000-0000-000000000003', N'MADINAH', N'منطقة المدينة المنورة', N'Madinah Region', 24.4672, 39.6024, 8, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
('10000000-0000-0000-0000-000000000004', N'EASTERN', N'المنطقة الشرقية', N'Eastern Region', 26.3927, 49.9777, 7, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
('10000000-0000-0000-0000-000000000005', N'QASSIM', N'منطقة القصيم', N'Qassim Region', 26.3267, 43.9650, 8, 5, SYSUTCDATETIME(), SYSUTCDATETIME()),
('10000000-0000-0000-0000-000000000006', N'HAIL', N'منطقة حائل', N'Hail Region', 27.5114, 41.7208, 8, 6, SYSUTCDATETIME(), SYSUTCDATETIME()),
('10000000-0000-0000-0000-000000000007', N'TABUK', N'منطقة تبوك', N'Tabuk Region', 28.3835, 36.5662, 7, 7, SYSUTCDATETIME(), SYSUTCDATETIME()),
('10000000-0000-0000-0000-000000000008', N'NORTHERN_BORDERS', N'منطقة الحدود الشمالية', N'Northern Borders', 30.9753, 41.0186, 7, 8, SYSUTCDATETIME(), SYSUTCDATETIME()),
('10000000-0000-0000-0000-000000000009', N'JAWF', N'منطقة الجوف', N'Al Jawf Region', 29.8868, 39.3206, 8, 9, SYSUTCDATETIME(), SYSUTCDATETIME()),
('10000000-0000-0000-0000-000000000010', N'JIZAN', N'منطقة جازان', N'Jizan Region', 16.8893, 42.5510, 9, 10, SYSUTCDATETIME(), SYSUTCDATETIME()),
('10000000-0000-0000-0000-000000000011', N'ASIR', N'منطقة عسير', N'Asir Region', 18.2164, 42.5053, 8, 11, SYSUTCDATETIME(), SYSUTCDATETIME()),
('10000000-0000-0000-0000-000000000012', N'BAHA', N'منطقة الباحة', N'Al Baha Region', 20.0000, 41.4667, 9, 12, SYSUTCDATETIME(), SYSUTCDATETIME()),
('10000000-0000-0000-0000-000000000013', N'NAJRAN', N'منطقة نجران', N'Najran Region', 17.4933, 44.1322, 8, 13, SYSUTCDATETIME(), SYSUTCDATETIME());

INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc]) VALUES
('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', N'RIYADH', N'الرياض', N'Riyadh', 24.7136, 46.6753, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', N'KHARJ', N'الخرج', N'Al Kharj', 24.1500, 47.3000, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000001', N'DAWADMI', N'الدوادمي', N'Ad Dawadmi', 24.5000, 44.3833, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000001', N'MAJMAAH', N'المجمعة', N'Al Majma''ah', 25.9000, 45.3500, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000005', '10000000-0000-0000-0000-000000000001', N'WADI_DAWASIR', N'وادي الدواسر', N'Wadi ad-Dawasir', 20.4500, 44.7833, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000006', '10000000-0000-0000-0000-000000000001', N'AFIF', N'عفيف', N'Afif', 23.9167, 42.9333, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000007', '10000000-0000-0000-0000-000000000001', N'SHAQRA', N'شقراء', N'Shaqra', 25.2500, 45.2500, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000008', '10000000-0000-0000-0000-000000000002', N'MAKKAH', N'مكة المكرمة', N'Makkah', 21.4225, 39.8262, 13, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000009', '10000000-0000-0000-0000-000000000002', N'JEDDAH', N'جدة', N'Jeddah', 21.5433, 39.1728, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000010', '10000000-0000-0000-0000-000000000002', N'TAIF', N'الطائف', N'Taif', 21.2703, 40.4159, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000011', '10000000-0000-0000-0000-000000000002', N'RABIGH', N'رابغ', N'Rabigh', 22.7985, 39.0350, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000012', '10000000-0000-0000-0000-000000000002', N'QUNFUDHAH', N'القنفذة', N'Al Qunfudhah', 19.1269, 41.0789, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000013', '10000000-0000-0000-0000-000000000003', N'MADINAH', N'المدينة المنورة', N'Madinah', 24.4672, 39.6024, 13, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000014', '10000000-0000-0000-0000-000000000003', N'YANBU', N'ينبع', N'Yanbu', 24.0886, 38.0633, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000015', '10000000-0000-0000-0000-000000000003', N'ULA', N'العلا', N'Al Ula', 26.6096, 37.9200, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000016', '10000000-0000-0000-0000-000000000003', N'BADR', N'بدر', N'Badr', 23.7831, 38.7885, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000017', '10000000-0000-0000-0000-000000000004', N'DAMMAM', N'الدمام', N'Dammam', 26.3927, 49.9777, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000018', '10000000-0000-0000-0000-000000000004', N'KHOBAR', N'الخبر', N'Al Khobar', 26.2172, 50.1971, 13, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000019', '10000000-0000-0000-0000-000000000004', N'DHAHRAN', N'الظهران', N'Dhahran', 26.2361, 50.0393, 13, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000020', '10000000-0000-0000-0000-000000000004', N'JUBAIL', N'الجبيل', N'Jubail', 27.0046, 49.6226, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000021', '10000000-0000-0000-0000-000000000004', N'QATIF', N'القطيف', N'Qatif', 26.5240, 50.0134, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000022', '10000000-0000-0000-0000-000000000004', N'HOFUF', N'الهفوف', N'Al Hofuf', 25.3809, 49.5866, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000023', '10000000-0000-0000-0000-000000000004', N'MUBARRAZ', N'المبرز', N'Al Mubarraz', 25.4282, 49.5614, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000024', '10000000-0000-0000-0000-000000000004', N'KHAFJI', N'الخفجي', N'Khafji', 28.4392, 48.4926, 12, 8, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000025', '10000000-0000-0000-0000-000000000005', N'BURAYDAH', N'بريدة', N'Buraydah', 26.3267, 43.9650, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000026', '10000000-0000-0000-0000-000000000005', N'UNAYZAH', N'عنيزة', N'Unayzah', 26.0842, 43.9887, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000027', '10000000-0000-0000-0000-000000000005', N'RASS', N'الرس', N'Ar Rass', 25.8523, 43.4946, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000028', '10000000-0000-0000-0000-000000000006', N'HAIL_CITY', N'حائل', N'Hail', 27.5114, 41.7208, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000029', '10000000-0000-0000-0000-000000000006', N'BAQAA', N'بقعاء', N'Baqa''a', 27.9000, 42.3833, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000030', '10000000-0000-0000-0000-000000000007', N'TABUK_CITY', N'تبوك', N'Tabuk', 28.3835, 36.5662, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000031', '10000000-0000-0000-0000-000000000007', N'WAJH', N'الوجه', N'Al Wajh', 26.2310, 36.4541, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000032', '10000000-0000-0000-0000-000000000007', N'DUBA', N'ضباء', N'Duba', 27.3491, 35.6987, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000033', '10000000-0000-0000-0000-000000000007', N'NEOM', N'نيوم', N'NEOM', 28.0000, 35.0000, 10, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000034', '10000000-0000-0000-0000-000000000008', N'ARAR', N'عرعر', N'Arar', 30.9753, 41.0186, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000035', '10000000-0000-0000-0000-000000000008', N'RAFHA', N'رفحاء', N'Rafha', 29.6208, 43.4932, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000036', '10000000-0000-0000-0000-000000000008', N'TURAIF', N'طريف', N'Turaif', 31.6716, 38.6554, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000037', '10000000-0000-0000-0000-000000000009', N'SAKAKA', N'سكاكا', N'Sakaka', 29.9697, 40.2064, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000038', '10000000-0000-0000-0000-000000000009', N'DUMAT_JANDAL', N'دومة الجندل', N'Dumat Al-Jandal', 29.8136, 39.8618, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000039', '10000000-0000-0000-0000-000000000009', N'QURAYAT', N'القريات', N'Qurayat', 31.3343, 37.3428, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000040', '10000000-0000-0000-0000-000000000010', N'JIZAN_CITY', N'جازان', N'Jizan', 16.8893, 42.5510, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000041', '10000000-0000-0000-0000-000000000010', N'SABYA', N'صبيا', N'Sabya', 17.1509, 42.6231, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000042', '10000000-0000-0000-0000-000000000010', N'ABU_ARISH', N'أبو عريش', N'Abu Arish', 16.9618, 42.8304, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000043', '10000000-0000-0000-0000-000000000011', N'ABHA', N'أبها', N'Abha', 18.2164, 42.5053, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000044', '10000000-0000-0000-0000-000000000011', N'KHAMIS_MUSHAIT', N'خميس مشيط', N'Khamis Mushait', 18.3000, 42.7333, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000045', '10000000-0000-0000-0000-000000000011', N'BISHA', N'بيشة', N'Bisha', 19.9833, 42.6000, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000046', '10000000-0000-0000-0000-000000000011', N'NAMAS', N'النماص', N'An Namas', 19.1189, 42.1304, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000047', '10000000-0000-0000-0000-000000000012', N'BAHA_CITY', N'الباحة', N'Al Baha', 20.0000, 41.4667, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000048', '10000000-0000-0000-0000-000000000012', N'BALJURASHI', N'بلجرشي', N'Baljurashi', 19.8500, 41.6167, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000049', '10000000-0000-0000-0000-000000000013', N'NAJRAN_CITY', N'نجران', N'Najran', 17.4933, 44.1322, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('20000000-0000-0000-0000-000000000050', '10000000-0000-0000-0000-000000000013', N'SHARURAH', N'شرورة', N'Sharurah', 17.4875, 47.1128, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME());
");
        }
    }
}
