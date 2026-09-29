using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoreHrModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    UniqueId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RegisteredName = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    GstNumber = table.Column<string>(type: "TEXT", maxLength: 15, nullable: true),
                    Address_Line1 = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Address_Line2 = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Address_City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address_State = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address_PinCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Address_Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LogoPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    IsHolidayCalendarApplicable = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.UniqueId);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    UniqueId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Address_Line1 = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Address_Line2 = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Address_City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address_State = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address_PinCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Address_Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GstNumber = table.Column<string>(type: "TEXT", maxLength: 15, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.UniqueId);
                    table.ForeignKey(
                        name: "FK_Branches_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveTypes",
                columns: table => new
                {
                    UniqueId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AnnualQuota = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: false),
                    IsPaid = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowCarryForward = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxCarryForward = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveTypes", x => x.UniqueId);
                    table.ForeignKey(
                        name: "FK_LeaveTypes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContactPersons",
                columns: table => new
                {
                    UniqueId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: false),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: true),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Designation = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AlternatePhone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPersons", x => x.UniqueId);
                    table.ForeignKey(
                        name: "FK_ContactPersons_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContactPersons_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    UniqueId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: false),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    IsOptional = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.UniqueId);
                    table.ForeignKey(
                        name: "FK_Holidays_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Holidays_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    UniqueId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: false),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    BreakMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    GraceMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    HalfDayMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    FullDayMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.UniqueId);
                    table.ForeignKey(
                        name: "FK_Shifts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shifts_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    UniqueId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: false),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShiftId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReportingManagerId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    EmpCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AttendanceId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Gender = table.Column<int>(type: "INTEGER", nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Mobile = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Pan = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Aadhaar = table.Column<string>(type: "TEXT", maxLength: 12, nullable: true),
                    PhotoPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    HighestQualification = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Department = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Designation = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DateOfJoining = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DateOfLeaving = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PriorExperienceMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.UniqueId);
                    table.ForeignKey(
                        name: "FK_Employees_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Employees_ReportingManagerId",
                        column: x => x.ReportingManagerId,
                        principalTable: "Employees",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    UniqueId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShiftId = table.Column<int>(type: "INTEGER", nullable: true),
                    AttendanceDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    LoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LogoffAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WorkedMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsLate = table.Column<bool>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.UniqueId);
                    table.ForeignKey(
                        name: "FK_Attendances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attendances_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BiometricIdAssignments",
                columns: table => new
                {
                    UniqueId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: false),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttendanceId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiometricIdAssignments", x => x.UniqueId);
                    table.ForeignKey(
                        name: "FK_BiometricIdAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BiometricIdAssignments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BiometricPunches",
                columns: table => new
                {
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AttendanceId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PunchTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsProcessed = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiometricPunches", x => x.UniqueId);
                    table.ForeignKey(
                        name: "FK_BiometricPunches_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BiometricPunches_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveBalances",
                columns: table => new
                {
                    UniqueId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Allocated = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: false),
                    CarriedForward = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: false),
                    Used = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveBalances", x => x.UniqueId);
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveBalances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveBalances_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveApplications",
                columns: table => new
                {
                    UniqueId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsHalfDay = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalDays = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    AppliedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovedById = table.Column<int>(type: "INTEGER", nullable: true),
                    ActionedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApproverRemarks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveApplications", x => x.UniqueId);
                    table.ForeignKey(
                        name: "FK_LeaveApplications_Employees_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Employees",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveApplications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveApplications_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EmployeeId_AttendanceDate_ShiftId",
                table: "Attendances",
                columns: new[] { "EmployeeId", "AttendanceDate", "ShiftId" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_ShiftId",
                table: "Attendances",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_BiometricIdAssignments_EmployeeId",
                table: "BiometricIdAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_BiometricIdAssignments_OrganizationId_AttendanceId",
                table: "BiometricIdAssignments",
                columns: new[] { "OrganizationId", "AttendanceId" },
                unique: true,
                filter: "\"ValidTo\" IS NULL AND \"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BiometricIdAssignments_OrganizationId_AttendanceId_ValidFrom",
                table: "BiometricIdAssignments",
                columns: new[] { "OrganizationId", "AttendanceId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_BiometricPunches_EmployeeId",
                table: "BiometricPunches",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_BiometricPunches_IsProcessed",
                table: "BiometricPunches",
                column: "IsProcessed");

            migrationBuilder.CreateIndex(
                name: "IX_BiometricPunches_OrganizationId_AttendanceId_PunchTime",
                table: "BiometricPunches",
                columns: new[] { "OrganizationId", "AttendanceId", "PunchTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Branches_OrganizationId_Code",
                table: "Branches",
                columns: new[] { "OrganizationId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_BranchId",
                table: "ContactPersons",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_OrganizationId",
                table: "ContactPersons",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveBalances_EmployeeId_LeaveTypeId_Year",
                table: "EmployeeLeaveBalances",
                columns: new[] { "EmployeeId", "LeaveTypeId", "Year" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveBalances_LeaveTypeId",
                table: "EmployeeLeaveBalances",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_BranchId",
                table: "Employees",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_OrganizationId_AttendanceId",
                table: "Employees",
                columns: new[] { "OrganizationId", "AttendanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_OrganizationId_EmpCode",
                table: "Employees",
                columns: new[] { "OrganizationId", "EmpCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ReportingManagerId",
                table: "Employees",
                column: "ReportingManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ShiftId",
                table: "Employees",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId",
                unique: true,
                filter: "\"UserId\" IS NOT NULL AND \"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_BranchId",
                table: "Holidays",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_OrganizationId_BranchId_Date",
                table: "Holidays",
                columns: new[] { "OrganizationId", "BranchId", "Date" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveApplications_ApprovedById",
                table: "LeaveApplications",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveApplications_EmployeeId_FromDate",
                table: "LeaveApplications",
                columns: new[] { "EmployeeId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveApplications_LeaveTypeId",
                table: "LeaveApplications",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_OrganizationId_Code",
                table: "LeaveTypes",
                columns: new[] { "OrganizationId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_BranchId",
                table: "Shifts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_OrganizationId_Code",
                table: "Shifts",
                columns: new[] { "OrganizationId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "BiometricIdAssignments");

            migrationBuilder.DropTable(
                name: "BiometricPunches");

            migrationBuilder.DropTable(
                name: "ContactPersons");

            migrationBuilder.DropTable(
                name: "EmployeeLeaveBalances");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "LeaveApplications");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "LeaveTypes");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "Organizations");
        }
    }
}
