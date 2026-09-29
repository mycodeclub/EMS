namespace EMS.Models;

public enum ContactRole { Primary = 1, Secondary, HelpDesk, Director, Manager, HR, Other }

public enum Gender { Male = 1, Female, Other }

public enum EmployeeStatus { Active = 1, OnProbation, OnNotice, Resigned, Terminated }

public enum LeaveStatus { Pending = 1, Approved, Rejected, Cancelled }

public enum AttendanceStatus { Present = 1, Absent, HalfDay, OnLeave, Holiday, WeeklyOff }

public enum AttendanceSource { Biometric = 1, Manual }
