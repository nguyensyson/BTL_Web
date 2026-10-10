using System.Text.Json;
using RestaurantOrdering.ViewModels.Waiter;

namespace RestaurantOrdering.Services;

/// <summary>
/// Quản lý giỏ nháp (DRAFT) trong Session, key = "cart_{tableId}".
/// </summary>
public class SessionCartService(IHttpContextAccessor httpContextAccessor)
{
    private static string Key(int tableId) => $"cart_{tableId}";

    public List<CartDraftItem> Get(int tableId)
    {
        var json = httpContextAccessor.HttpContext?.Session.GetString(Key(tableId));
        if (string.IsNullOrEmpty(json)) return new();
        return JsonSerializer.Deserialize<List<CartDraftItem>>(json) ?? new();
    }

    public void Save(int tableId, List<CartDraftItem> items)
    {
        var json = JsonSerializer.Serialize(items);
        httpContextAccessor.HttpContext?.Session.SetString(Key(tableId), json);
    }

    public void Clear(int tableId)
        => httpContextAccessor.HttpContext?.Session.Remove(Key(tableId));

    public CartDraftItem? FindItem(int tableId, Guid localId)
        => Get(tableId).FirstOrDefault(i => i.LocalId == localId);

    public void AddOrUpdate(int tableId, CartDraftItem item)
    {
        var list = Get(tableId);
        var idx = list.FindIndex(i => i.LocalId == item.LocalId);
        if (idx >= 0) list[idx] = item;
        else list.Add(item);
        Save(tableId, list);
    }

    public bool Remove(int tableId, Guid localId)
    {
        var list = Get(tableId);
        var removed = list.RemoveAll(i => i.LocalId == localId) > 0;
        if (removed) Save(tableId, list);
        return removed;
    }
}
