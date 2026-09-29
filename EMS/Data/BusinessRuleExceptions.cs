namespace EMS.Data;

/// <summary>A save was refused by a business rule. The message is safe to show to the user.</summary>
public class BusinessRuleException(string message) : Exception(message);

/// <summary>Deleting a record that is still referenced by active (not deleted) records.</summary>
public class DeleteBlockedException(string entityName, object key, string childName, int activeCount)
    : BusinessRuleException($"Cannot delete {entityName} #{key}: it still has {activeCount} active {childName} record(s). Delete or reassign them first.");

/// <summary>Restoring a record whose parent is still deleted.</summary>
public class RestoreBlockedException(string entityName, object key, string parentName, object parentKey)
    : BusinessRuleException($"Cannot restore {entityName} #{key}: its {parentName} #{parentKey} is deleted. Restore that first.");
