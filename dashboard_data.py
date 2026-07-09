import sqlite3

import pandas as pd
import streamlit as st

from tracker import DB_NAME, ProductivityTracker


def load_activity_data(tracker: ProductivityTracker) -> pd.DataFrame:
    """Carrega dados do SQLite e faz pré-processamento."""
    try:
        conn = sqlite3.connect(DB_NAME)
        df = pd.read_sql_query("SELECT * FROM activity_log", conn)
        conn.close()

        if df.empty:
            return pd.DataFrame()

        app_settings = tracker.get_app_settings()

        df["display_name"] = df["app_name"].map(
            lambda name: app_settings.get(name, {}).get("display_name", name)
        )
        df["hex_color"] = df["app_name"].map(
            lambda name: app_settings.get(name, {}).get("hex_color")
        )
        df["category"] = df["app_name"].map(
            lambda name: app_settings.get(name, {}).get("category", "Sem Categoria")
        )

        df["start_time"] = pd.to_datetime(df["start_time"], format="mixed", errors="coerce")
        df["end_time"] = pd.to_datetime(df["end_time"], format="mixed", errors="coerce")
        df = df.dropna(subset=["start_time"])

        df["date"] = df["start_time"].dt.date
        df["hour"] = df["start_time"].dt.hour
        df["category"] = df["category"].fillna("Sem Categoria")

        return df
    except Exception as e:
        st.error(f"Erro ao carregar banco de dados: {e}")
        return pd.DataFrame()
