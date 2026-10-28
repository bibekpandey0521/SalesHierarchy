using System.Net.Mail;

namespace SalesHierarchy.Core.Validation
{
    /// <summary>
    /// Validation rules for a single Excel bulk-upload row. Kept in the Core project
    /// (and free of data access) so the same rules are used by the web layer and by tests.
    /// </summary>
    public static class BulkRowValidator
    {
        public const int MaxNameLength = 200;
        public const int MaxDesignationLength = 100;
        public const int MaxEmailLength = 256;

        /// <summary>
        /// Validates the field-level rules of a row. Manager lookup and duplicate
        /// detection need data access and are handled by the caller.
        /// </summary>
        public static IReadOnlyList<string> ValidateFields(string? name, string? designation, string? email, string? managerEmail)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("Name is required.");
            }
            else if (name.Trim().Length > MaxNameLength)
            {
                errors.Add($"Name must be {MaxNameLength} characters or fewer.");
            }

            if (string.IsNullOrWhiteSpace(designation))
            {
                errors.Add("Designation is required.");
            }
            else if (designation.Trim().Length > MaxDesignationLength)
            {
                errors.Add($"Designation must be {MaxDesignationLength} characters or fewer.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                errors.Add("Email is required.");
            }
            else if (!IsValidEmail(email))
            {
                errors.Add("Email is not a valid address.");
            }

            if (!string.IsNullOrWhiteSpace(managerEmail) && !IsValidEmail(managerEmail))
            {
                errors.Add("Manager email is not a valid address.");
            }

            return errors;
        }

        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > MaxEmailLength)
            {
                return false;
            }

            if (!MailAddress.TryCreate(email.Trim(), out var parsed))
            {
                return false;
            }

            // MailAddress accepts host names such as "tld"; require a real dotted domain
            // so mistyped addresses are rejected during bulk upload.
            var host = parsed.Host;
            return host.Contains('.') && !host.StartsWith('.') && !host.EndsWith('.') && !host.Contains("..");
        }

        public static bool IsSelfReport(string? email, string? managerEmail)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(managerEmail))
            {
                return false;
            }

            return Normalize(email) == Normalize(managerEmail);
        }

        /// <summary>
        /// Walks the in-file manager chain starting at <paramref name="email"/> and returns
        /// true when the chain loops back to the starting row.
        /// </summary>
        public static bool WouldCreateCycle(string email, string? managerEmail, IReadOnlyDictionary<string, string> managerEmailByEmail)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(managerEmail))
            {
                return false;
            }

            var start = Normalize(email);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = Normalize(managerEmail);

            while (true)
            {
                if (current == start)
                {
                    return true;
                }

                if (!visited.Add(current))
                {
                    // A cycle exists elsewhere in the file; refuse this row too.
                    return true;
                }

                if (!managerEmailByEmail.TryGetValue(current, out var next) || string.IsNullOrWhiteSpace(next))
                {
                    return false;
                }

                current = Normalize(next);
            }
        }

        public static string Normalize(string value) => value.Trim().ToLowerInvariant();
    }
}
