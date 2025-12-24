import streamlit as st
import os

# Lista pré-definida de categorias
CATEGORIES = [
    "Sem Categoria",
    "Trabalho",
    "Estudo",
    "Desenvolvimento",
    "Comunicação",
    "Lazer",
    "Navegação",
    "Utilitários",
    "Outros"
]

def render_settings_ui(tracker):
    """Renderiza a interface de configuração de aplicativos na Sidebar."""
    with st.sidebar.expander("⚙️ Personalizar Apps"):
        st.caption("Defina nomes amigáveis, cores e categorias.")
        
        # Carregar dados
        all_apps = tracker.get_all_apps()
        current_settings = tracker.get_app_settings()
        
        if not all_apps:
            st.info("Nenhum app registrado.")
            return

        # Seleção do App
        selected_app = st.selectbox("Selecione o App", all_apps)
        
        if selected_app:
            # Valores atuais
            current_config = current_settings.get(selected_app, {})
            
            with st.form(key=f"form_{selected_app}"):
                # Mudança: Usando display_name em vez de pretty_name
                new_display = st.text_input(
                    "Nome de Exibição", 
                    value=current_config.get("display_name", selected_app)
                )
                
                # Categoria (Selectbox)
                curr_cat = current_config.get("category")
                if curr_cat not in CATEGORIES:
                    curr_cat = "Sem Categoria"
                    
                new_category = st.selectbox(
                    "Categoria",
                    CATEGORIES,
                    index=CATEGORIES.index(curr_cat)
                )

                # Layout simplificado sem ícone
                default_color = current_config.get("hex_color") or "#808080"
                new_color = st.color_picker("Cor de Exibição", value=default_color)

                if st.form_submit_button("💾 Salvar"):
                    # Passamos os novos parâmetros atualizados (sem ícone)
                    if tracker.update_app_setting(selected_app, new_display, new_color, new_category):
                        st.success("Salvo!")
                        st.rerun()