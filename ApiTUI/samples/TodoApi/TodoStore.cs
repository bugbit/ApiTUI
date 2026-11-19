namespace TodoApi;

internal sealed class TodoStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, TodoTask> _tasks = [with(capacity: 16)];
    private int _nextId;

    public void Seed()
    {
        Add("Probar ApiTUI");
        Add("Revisar documentación", isCompleted: true);
        Add("Preparar demo del TFM");
    }

    public IReadOnlyList<TodoTask> GetAll()
    {
        lock (_lock)
        {
            return _tasks.Values.OrderBy(task => task.Id).ToArray();
        }
    }

    public FindTaskResult Find(int id)
    {
        lock (_lock)
        {
            return _tasks.TryGetValue(id, out var task) ? task : new TaskNotFound(id);
        }
    }

    public TodoTask Add(string title, bool isCompleted = false)
    {
        lock (_lock)
        {
            var task = new TodoTask(++_nextId, title, isCompleted);
            _tasks.Add(task.Id, task);
            return task;
        }
    }

    public FindTaskResult Update(int id, string title, bool isCompleted)
    {
        lock (_lock)
        {
            if (!_tasks.ContainsKey(id))
            {
                return new TaskNotFound(id);
            }

            var task = new TodoTask(id, title, isCompleted);
            _tasks[id] = task;
            return task;
        }
    }

    public RemoveTaskResult Remove(int id)
    {
        lock (_lock)
        {
            return _tasks.Remove(id) ? new TaskRemoved(id) : new TaskNotFound(id);
        }
    }
}
