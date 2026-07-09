import streamlit as st

from tracker import ProductivityTracker
import dashboard_settings

from dashboard_data import load_activity_data
from dashboard_details import render_details_tab
from dashboard_filters import render_date_filter
from dashboard_overview import render_overview_tab
from dashboard_utils import build_color_map

st.set_page_config(
    page_title="Monitor de Produtividade",
    layout="wide",
    page_icon="⏱️",
    initial_sidebar_state="collapsed",
)


def main():
    st.title("📊 Painel de Produtividade Pessoal")

    if "limit_apps" not in st.session_state:
        st.session_state["limit_apps"] = 5

    tracker = ProductivityTracker()
    df_raw = load_activity_data(tracker)

    if df_raw.empty:
        st.warning("Nenhum dado encontrado. Certifique-se de que o 'tracker.py' está rodando.")
        st.stop()

    st.sidebar.header("Filtros")

    available_dates = sorted(df_raw["date"].unique(), reverse=True)
    if not available_dates:
        st.sidebar.write("Sem datas disponíveis.")
        st.stop()

    selected_date, has_data = render_date_filter(available_dates)
    df = df_raw[df_raw["date"] == selected_date].copy() if has_data else df_raw.iloc[0:0].copy()
    color_map = build_color_map(df)

    if st.sidebar.button("Atualizar Dados"):
        st.rerun()

    tab_overview, tab_details, tab_customize = st.tabs([
        "🏠 Visão Geral",
        "🔍 Detalhes por App (Abas)",
        "⚙️ Personalizar Apps",
    ])

    with tab_overview:
        render_overview_tab(df, color_map)

    with tab_details:
        render_details_tab(df)

    with tab_customize:
        dashboard_settings.render_settings_tab(tracker)


if __name__ == "__main__":
    main()
