import streamlit as st

CATEGORIES = [
    "Sem Categoria",
    "Trabalho",
    "Estudo",
    "Desenvolvimento",
    "Comunicação",
    "Lazer",
    "Navegação",
    "Utilitários",
    "Outros",
]


def _color_swatch(hex_color: str) -> str:
    return (
        f'<div style="width:32px;height:32px;background-color:{hex_color};'
        f'border:1px solid rgba(128,128,128,0.45);border-radius:6px;"></div>'
    )


def render_settings_tab(tracker):
    """Renderiza a interface de personalização de apps em uma aba do dashboard."""
    st.header("⚙️ Personalizar Apps")
    st.caption("Defina nomes amigáveis, cores e categorias para cada aplicativo.")

    all_apps = tracker.get_all_apps()
    current_settings = tracker.get_app_settings()

    if not all_apps:
        st.info("Nenhum app registrado.")
        return

    search = st.text_input(
        "Buscar app",
        placeholder="Filtrar por nome do executável ou nome de exibição...",
        key="settings_search",
    )
    search_lower = search.strip().lower()

    def _matches(app_name: str) -> bool:
        if not search_lower:
            return True
        display = current_settings.get(app_name, {}).get("display_name", app_name)
        return search_lower in app_name.lower() or search_lower in display.lower()

    filtered_apps = [app for app in all_apps if _matches(app)]

    if not filtered_apps:
        st.warning("Nenhum app corresponde à busca.")
        return

    st.caption(f"{len(filtered_apps)} app(s) exibido(s)")

    header = st.columns([2.2, 2.2, 2, 0.45, 1.1])
    header[0].markdown("**App**")
    header[1].markdown("**Nome de exibição**")
    header[2].markdown("**Categoria**")
    header[3].markdown("**Cor**")
    header[4].markdown("**Ajustar**")

    st.divider()

    pending_changes = {}

    for app_name in filtered_apps:
        config = current_settings.get(app_name, {})
        default_display = config.get("display_name", app_name)
        default_color = config.get("hex_color") or "#808080"
        curr_cat = config.get("category", "Sem Categoria")
        if curr_cat not in CATEGORIES:
            curr_cat = "Sem Categoria"

        color_key = f"cfg_color_{app_name}"
        preview_color = st.session_state.get(color_key, default_color)

        cols = st.columns([2.2, 2.2, 2, 0.45, 1.1])
        with cols[0]:
            st.text(app_name)
        with cols[1]:
            new_display = st.text_input(
                "Nome de exibição",
                value=default_display,
                key=f"cfg_display_{app_name}",
                label_visibility="collapsed",
            )
        with cols[2]:
            new_category = st.selectbox(
                "Categoria",
                CATEGORIES,
                index=CATEGORIES.index(curr_cat),
                key=f"cfg_cat_{app_name}",
                label_visibility="collapsed",
            )
        with cols[3]:
            st.markdown(_color_swatch(preview_color), unsafe_allow_html=True)
        with cols[4]:
            new_color = st.color_picker(
                "Cor",
                value=preview_color,
                key=color_key,
                label_visibility="collapsed",
            )

        pending_changes[app_name] = (new_display, new_color, new_category)

    st.divider()

    if st.button("💾 Salvar alterações", type="primary", key="save_app_settings"):
        saved = 0
        for app_name, (display, color, category) in pending_changes.items():
            old = current_settings.get(app_name, {})
            old_display = old.get("display_name", app_name)
            old_color = old.get("hex_color") or "#808080"
            old_cat = old.get("category", "Sem Categoria")
            if display != old_display or color != old_color or category != old_cat:
                if tracker.update_app_setting(app_name, display, color, category):
                    saved += 1

        if saved:
            st.success(f"{saved} app(s) atualizado(s)!")
            st.rerun()
        else:
            st.info("Nenhuma alteração para salvar.")
