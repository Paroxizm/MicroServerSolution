using FluentAssertions;

namespace MicroServer.Tests;

/// <summary>
/// Тесты хранилища (<see cref="SimpleStore"/>)
/// </summary>
public class SimpleStoreTests
{
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