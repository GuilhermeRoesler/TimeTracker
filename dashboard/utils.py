import pandas as pd


def format_duration_clean(seconds) -> str:
    if pd.isna(seconds):
        return "0m"
    h = int(seconds // 3600)
    m = int((seconds % 3600) // 60)
    if h > 0:
        return f"{h}h {m}m"
    return f"{m}m"


def clean_window_title(title) -> str:
    """Remove sufixos comuns de navegadores para limpar o gráfico."""
    if not title:
        return "Sem Título"

    suffixes_to_remove = [
        " - Opera",
        " - Google Chrome",
        " - Microsoft Edge",
        " - Mozilla Firefox",
        " - Brave",
        " - Vivaldi",
        " - YouTube",
    ]

    clean = str(title)
    for suffix in suffixes_to_remove:
        if suffix in clean:
            clean = clean.replace(suffix, "")

    return clean


def build_color_map(df: pd.DataFrame) -> dict:
    color_map = {}
    if "hex_color" not in df.columns:
        return color_map

    settings_df = df[["display_name", "hex_color"]].drop_duplicates().dropna()
    for _, row in settings_df.iterrows():
        if row["hex_color"]:
            color_map[row["display_name"]] = row["hex_color"]
    return color_map
