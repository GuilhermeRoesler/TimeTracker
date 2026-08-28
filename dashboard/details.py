import streamlit as st

from dashboard.charts import create_window_titles_chart
from dashboard.utils import clean_window_title, format_duration_clean


def _default_app_index(apps_list):
    for i, app in enumerate(apps_list):
        if "opera" in app.lower():
            return i
    return 0


def render_details_tab(df):
    st.header("🔎 O que você fez dentro de cada App?")
    st.caption("Selecione um aplicativo (como o Opera) para ver em quais abas ou arquivos você passou mais tempo.")

    apps_list = (
        df.groupby("display_name")["duration_seconds"]
        .sum()
        .sort_values(ascending=False)
        .index.tolist()
    )

    selected_app = st.selectbox(
        "Selecione o Aplicativo:",
        apps_list,
        index=_default_app_index(apps_list),
    )

    if not selected_app:
        return

    df_app = df[df["display_name"] == selected_app].copy()
    df_app["clean_title"] = df_app["window_title"].apply(clean_window_title)

    title_usage = (
        df_app.groupby("clean_title")["duration_seconds"]
        .sum()
        .sort_values(ascending=True)
        .tail(15)
    )

    col_chart, col_history = st.columns([2, 1])

    with col_chart:
        st.subheader(f"Top Abas/Janelas em: {selected_app}")
        if not title_usage.empty:
            title_usage_df = title_usage.reset_index()
            title_usage_df["formatted_time"] = title_usage_df["duration_seconds"].apply(format_duration_clean)
            fig = create_window_titles_chart(title_usage_df)
            if fig:
                st.plotly_chart(fig, use_container_width=True)
        else:
            st.info("Sem dados detalhados.")

    with col_history:
        st.subheader("Histórico Cronológico")
        history_df = df_app[["start_time", "clean_title", "duration_seconds"]].sort_values(
            by="start_time", ascending=False
        )
        history_df["Hora"] = history_df["start_time"].dt.strftime("%H:%M")
        history_df["Duração"] = history_df["duration_seconds"].apply(format_duration_clean)

        st.dataframe(
            history_df[["Hora", "clean_title", "Duração"]],
            use_container_width=True,
            hide_index=True,
            height=500,
        )
