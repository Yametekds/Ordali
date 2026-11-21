using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Ordali.Api.Common;

public static class DatabaseErrors
{
    /// <summary>
    /// True when the save failed because two requests wrote the same row or the same unique key
    /// at once. Anything else (a value too long, a broken foreign key, the server down) is a bug
    /// or an outage, not a conflict the user can fix by trying again.
    /// </summary>
    public static bool IsWriteConflict(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException || IsUniqueKeyViolation(exception);

    // 2601: duplicate key in a unique index; 2627: duplicate key in a unique constraint.
    private static bool IsUniqueKeyViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
