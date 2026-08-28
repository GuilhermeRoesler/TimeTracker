from datetime import date, timedelta

import streamlit as st


def render_date_filter(available_dates):
    """Seletor de data com calendário, atalhos e navegação entre dias com registros."""
    min_date = min(available_dates)
    max_date = max(available_dates)

    if "selected_date" not in st.session_state:
        st.session_state.selected_date = available_dates[0]
    elif st.session_state.selected_date < min_date or st.session_state.selected_date > max_date:
        st.session_state.selected_date = available_dates[0]

    shortcuts = []
    today = date.today()
    yesterday = today - timedelta(days=1)
    if today in available_dates:
        shortcuts.append(("Hoje", today))
    if yesterday in available_dates:
        shortcuts.append(("Ontem", yesterday))

    if shortcuts:
        shortcut_cols = st.sidebar.columns(len(shortcuts))
        for col, (label, shortcut_date) in zip(shortcut_cols, shortcuts):
            with col:
                if st.button(label, key=f"date_shortcut_{label}", use_container_width=True):
                    st.session_state.selected_date = shortcut_date
                    st.rerun()

    in_available = st.session_state.selected_date in available_dates
    current_idx = available_dates.index(st.session_state.selected_date) if in_available else 0

    col_prev, col_date, col_next = st.sidebar.columns([1, 5, 1])

    with col_prev:
        can_go_prev = in_available and current_idx < len(available_dates) - 1
        if st.button("◀", key="prev_day", disabled=not can_go_prev, help="Dia anterior com registros"):
            st.session_state.selected_date = available_dates[current_idx + 1]
            st.rerun()

    with col_date:
        st.date_input(
            "Selecione a Data",
            min_value=min_date,
            max_value=max_date,
            format="DD/MM/YYYY",
            key="selected_date",
            label_visibility="collapsed",
        )

    with col_next:
        can_go_next = in_available and current_idx > 0
        if st.button("▶", key="next_day", disabled=not can_go_next, help="Próximo dia com registros"):
            st.session_state.selected_date = available_dates[current_idx - 1]
            st.rerun()

    selected_date = st.session_state.selected_date
    has_data = selected_date in available_dates

    if not has_data:
        st.sidebar.warning("Sem registros nesta data.")

    return selected_date, has_data
