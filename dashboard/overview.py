import streamlit as st

from dashboard.charts import (
    create_app_ranking,
    create_category_pie,
    create_hourly_timeline,
    create_top_apps_donut,
)
from dashboard.utils import format_duration_clean


def render_overview_tab(df, color_map):
    total_seconds = df["duration_seconds"].sum()
    hours = int(total_seconds // 3600)
    minutes = int((total_seconds % 3600) // 60)

    col1, col2, col3 = st.columns(3)
    with col1:
        st.metric("Tempo Total", f"{hours}h {minutes}m")
    with col2:
        st.metric("Sessões (Focos)", len(df))
    with col3:
        usage_by_app = df.groupby("display_name")["duration_seconds"].sum().sort_values(ascending=False)
        if not usage_by_app.empty:
            st.metric("App Mais Usado", usage_by_app.index[0])

    st.markdown("---")

    row1_col1, row1_col2 = st.columns(2)

    with row1_col1:
        st.subheader("Distribuição (Top 5)")
        fig_donut = create_top_apps_donut(df, color_map)
        if fig_donut:
            st.plotly_chart(fig_donut, use_container_width=True)
        else:
            st.info("Sem dados.")

    with row1_col2:
        st.subheader("Linha do Tempo")
        fig_bar = create_hourly_timeline(df, color_map)
        if fig_bar:
            st.plotly_chart(fig_bar, use_container_width=True, key="grafico1")
        else:
            st.info("Sem atividades.")

    st.markdown("---")

    row2_col1, row2_col2 = st.columns(2)

    with row2_col1:
        st.subheader("Ranking Detalhado")
        fig_ranking, app_usage_all = create_app_ranking(df, color_map, st.session_state["limit_apps"])
        if fig_ranking:
            st.plotly_chart(fig_ranking, use_container_width=True)
            if len(app_usage_all) > st.session_state["limit_apps"]:
                if st.button("➕ Mostrar Mais 5", key="btn_more"):
                    st.session_state["limit_apps"] += 5
                    st.rerun()
        else:
            st.info("Sem dados.")

    with row2_col2:
        st.subheader("Categorias")
        fig_cat = create_category_pie(df)
        if fig_cat:
            st.plotly_chart(fig_cat, use_container_width=True)
        elif "category" in df.columns:
            st.info("Sem dados de categoria.")
        else:
            st.empty()

    st.subheader("Linha do Tempo")
    fig_timeline_full = create_hourly_timeline(df, color_map, height=500)
    if fig_timeline_full:
        st.plotly_chart(fig_timeline_full, use_container_width=True, key="grafico2")
    else:
        st.info("Sem atividades.")

    st.markdown("---")
    st.subheader("Histórico Detalhado")

    display_df = df[["start_time", "end_time", "display_name", "window_title", "duration_seconds", "category"]].copy()
    display_df["duration_str"] = display_df["duration_seconds"].apply(lambda x: f"{int(x // 60)}m {int(x % 60)}s")
    display_df = display_df.sort_values(by="start_time", ascending=False)

    st.dataframe(
        display_df[["start_time", "display_name", "category", "window_title", "duration_str"]],
        use_container_width=True,
        hide_index=True,
    )
