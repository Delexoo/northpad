using System.Globalization;
using Microsoft.Data.Sqlite;
using Northpad.Core.Cryptography;
using Northpad.Core.Models;
using Northpad.Core.Vault;

namespace Northpad.Core.Storage;

public interface ITaskRepository
{
    IReadOnlyList<TaskRecord> List();

    TaskRecord? Find(Guid id);

    TaskRecord Create(string title, string details, DateOnly? dueDate, TaskPriority priority);

    TaskRecord Update(TaskRecord task);

    void Delete(Guid id);
}

public sealed class TaskRepository : ITaskRepository
{
    private readonly SqliteDatabase _database;
    private readonly IContentProtector _protector;

    public TaskRepository(SqliteDatabase database, IContentProtector protector)
    {
        _database = database;
        _protector = protector;
    }

    public IReadOnlyList<TaskRecord> List()
    {
        return _database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, title, details, due_date, priority, is_completed, created_utc, updated_utc
                FROM tasks
                ORDER BY is_completed ASC, updated_utc DESC;
                """;
            using var reader = command.ExecuteReader();
            var tasks = new List<TaskRecord>();
            while (reader.Read())
            {
                tasks.Add(Read(reader));
            }

            return (IReadOnlyList<TaskRecord>)tasks;
        });
    }

    public TaskRecord? Find(Guid id)
    {
        return _database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, title, details, due_date, priority, is_completed, created_utc, updated_utc
                FROM tasks
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            using var reader = command.ExecuteReader();
            return reader.Read() ? Read(reader) : null;
        });
    }

    public TaskRecord Create(string title, string details, DateOnly? dueDate, TaskPriority priority)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new TaskRecord(Guid.NewGuid(), title, details, dueDate, priority, false, now, now);
        Write(task, insert: true);
        return task;
    }

    public TaskRecord Update(TaskRecord task)
    {
        if (Find(task.Id) is null)
        {
            throw new StorageException("The task could not be found.");
        }

        var updated = task with { UpdatedUtc = DateTimeOffset.UtcNow };
        Write(updated, insert: false);
        return updated;
    }

    public void Delete(Guid id)
    {
        _database.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM tasks WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            command.ExecuteNonQuery();
            transaction.Commit();
        });
    }

    private void Write(TaskRecord task, bool insert)
    {
        _database.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = insert
                ? """
                  INSERT INTO tasks (id, title, details, due_date, priority, is_completed, created_utc, updated_utc)
                  VALUES ($id, $title, $details, $due, $priority, $completed, $created, $updated);
                  """
                : """
                  UPDATE tasks
                  SET title = $title,
                      details = $details,
                      due_date = $due,
                      priority = $priority,
                      is_completed = $completed,
                      updated_utc = $updated
                  WHERE id = $id;
                  """;
            command.Parameters.AddWithValue("$id", task.Id.ToString("D"));
            command.Parameters.Add("$title", SqliteType.Blob).Value = _protector.Protect(task.Title, FieldContext.TaskTitle(task.Id));
            command.Parameters.Add("$details", SqliteType.Blob).Value = _protector.Protect(task.Details, FieldContext.TaskDetails(task.Id));
            command.Parameters.AddWithValue("$due", (object?)task.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value);
            command.Parameters.AddWithValue("$priority", (int)task.Priority);
            command.Parameters.AddWithValue("$completed", task.IsCompleted ? 1 : 0);
            command.Parameters.AddWithValue("$created", task.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$updated", task.UpdatedUtc.ToString("O", CultureInfo.InvariantCulture));
            if (command.ExecuteNonQuery() != 1)
            {
                throw new StorageException("The task could not be saved.");
            }

            transaction.Commit();
        });
    }

    private TaskRecord Read(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var title = _protector.Unprotect((byte[])reader.GetValue(1), FieldContext.TaskTitle(id));
        var details = _protector.Unprotect((byte[])reader.GetValue(2), FieldContext.TaskDetails(id));
        DateOnly? due = reader.IsDBNull(3)
            ? null
            : DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var priority = (TaskPriority)reader.GetInt64(4);
        var completed = reader.GetInt64(5) != 0;
        var created = DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var updated = DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new TaskRecord(id, title, details, due, priority, completed, created, updated);
    }
}
