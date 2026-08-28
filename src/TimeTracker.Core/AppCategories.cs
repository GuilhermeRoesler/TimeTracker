namespace TimeTracker.Core;

public static class AppCategories
{
    public static readonly IReadOnlyList<string> All =
    [
        "Sem Categoria",
        "Trabalho",
        "Estudo",
        "Desenvolvimento",
        "Comunicação",
        "Lazer",
        "Navegação",
        "Utilitários",
        "Outros",
    ];

    public static string Normalize(string? category)
        => string.IsNullOrWhiteSpace(category) || !All.Contains(category)
            ? "Sem Categoria"
            : category;
}
