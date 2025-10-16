using FluentAssertions;
using Xunit.Abstractions;

namespace MicroServer.Tests;

/// <summary>
/// Тесты хранилища (<see cref="SimpleStore"/>)
/// </summary>
public class SimpleStoreTests(ITestOutputHelper logger)
{
    private class TaskCountVariations : TheoryData<int, int, int>
    {
        public TaskCountVariations()
        {
            Add(0, 10000, 10);
            Add(0, 1000, 100);
            Add(0, 100, 1000);
            Add(0, 10, 10000);
            Add(10, 10000, 10);
            Add(100, 1000, 100);
            Add(1000, 100, 1000);
            Add(10000, 10, 10000);
            Add(10, 0, 10);
            Add(100, 0, 100);
            Add(1000, 0, 1000);
            Add(10000, 0, 10000);
            Add(10, 10000, 0);
            Add(100, 1000, 0);
            Add(1000, 100, 0);
            Add(10000, 10, 0);
        }
        
    }
    
    private List<Task> CreateTasks(SimpleStore storage, int getTasks, int setTasks, int removeTasks)
    {
        var tasks = new List<Task>();

        for (var i = 1; i <= getTasks; i++)
        {
            var key = $"KEY-{i}";
            tasks.Add(new Task(() => storage.Get(key)));
        }

        for (var i = 1; i <= setTasks; i++)
        {
            var len = i;
            var key = $"KEY-{len}";
            tasks.Add(new Task(() => storage.Set(key, new byte[len])));
        }

        for (var i = 1; i <= removeTasks; i++)
        {
            var key = $"KEY-{i}";
            tasks.Add(new Task(() => storage.Delete(key)));
        }

        Parallel.ForEach(tasks, task => task.Start());
        return tasks;
    }

    [Theory]
    [InlineData(1, 10000)]
    [InlineData(10, 1000)]
    [InlineData(100, 100)]
    [InlineData(1000, 10)]
    [InlineData(10000, 1)]
    public async Task Parallel_Get_Set_Should_Set_Correct_Data(int getTasks, int setTasks)
    {
        var storage = new SimpleStore();

        var exception = await Record.ExceptionAsync(async () => await Task.WhenAll(
            CreateTasks(storage, getTasks, setTasks, 0)
        ));
        exception.Should().BeNull();

        var data = storage.GetAll();

        data.Should()
            .HaveCount(setTasks)
            .And.Match(x => x.All(p => p.Value.Length == int.Parse(p.Key.Split('-', StringSplitOptions.None)[1])));
    }
    
    [Theory]
    [ClassData(typeof(TaskCountVariations))]
    public async Task Parallel_Calls_Should_Not_Throw_Race_Condition(int getTasks, int setTasks, int deleteTasks)
    {
        var storage = new SimpleStore();

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(
            CreateTasks(storage, getTasks, setTasks, deleteTasks)
        ));
        exception.Should().BeNull();
    }

    [Theory]
    [ClassData(typeof(TaskCountVariations))]
    public async Task Parallel_Calls_Should_Set_Valid_Counters(int getTasks, int setTasks, int removeTasks)
    {
        var storage = new SimpleStore();

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(
            CreateTasks(storage, getTasks, setTasks, removeTasks)
        ));
        exception.Should().BeNull();

        var (getCount, setCount, deleteCount) = storage.GetStatistic();
        deleteCount.Should().Be(removeTasks);
        getCount.Should().Be(getTasks);
        setCount.Should().Be(setTasks);
    }

    /// <summary>
    /// Тест простого добавления элемента
    /// </summary>
    [Fact]
    public void Should_Add_Item()
    {
        var store = new SimpleStore();
        var value = "value-1"u8.ToArray();

        store.Set("key-1", value);

        var gotValue = store.Get("key-1");
        gotValue.Should()
            .NotBeNullOrEmpty()
            .And.BeEquivalentTo(value);
    }

    /// <summary>
    /// Тест простого обновления элемента
    /// </summary>
    [Fact]
    public void Should_Update_Item()
    {
        var store = new SimpleStore();
        var originalValue = "value-1"u8.ToArray();
        var newValue = "value-2"u8.ToArray();

        store.Set("key-1", originalValue);

        var gotValue = store.Get("key-1");
        gotValue.Should()
            .NotBeNullOrEmpty()
            .And.BeEquivalentTo(originalValue);

        store.Set("key-1", newValue);

        gotValue = store.Get("key-1");
        gotValue.Should()
            .NotBeNullOrEmpty()
            .And.BeEquivalentTo(newValue);
    }

    /// <summary>
    /// Тест простого удаления элемента
    /// </summary>
    [Fact]
    public void Should_Delete_Item()
    {
        var store = new SimpleStore();
        var originalValue = "value-1"u8.ToArray();
        store.Set("key-1", originalValue);

        var gotValue = store.Get("key-1");
        gotValue.Should()
            .NotBeNullOrEmpty()
            .And.BeEquivalentTo(originalValue);

        store.Delete("key-1");
        gotValue = store.Get("key-1");
        gotValue.Should()
            .BeNull();
    }
}