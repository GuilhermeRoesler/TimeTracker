import streamlit as st
import pandas as pd
import sqlite3
import os
import plotly.express as px
import plotly.graph_objects as go
from datetime import date, datetime, timedelta

from tracker import ProductivityTracker
import settings_ui

# Configuração da Página
st.set_page_config(page_title="Monitor de Produtividade", layout="wide", page_icon="⏱️")

DB_NAME = "productivity.db"

# --- Funções do Banco de Dados ---

def load_data(tracker: ProductivityTracker):
    """Carrega dados do SQLite e faz pré-processamento."""
    try:
        conn = sqlite3.connect(DB_NAME)
        df = pd.read_sql_query("SELECT * FROM activity_log", conn)
        conn.close()

        if df.empty:
            return pd.DataFrame()

        app_settings = tracker.get_app_settings()

        df['display_name'] = df['app_name'].map(
            lambda name: app_settings.get(name, {}).get("display_name", name)
        )
        df['hex_color'] = df['app_name'].map(
            lambda name: app_settings.get(name, {}).get("hex_color")
        )
        df['category'] = df['app_name'].map(
            lambda name: app_settings.get(name, {}).get("category", "Sem Categoria")
        )

        df['start_time'] = pd.to_datetime(df['start_time'], format='mixed', errors='coerce')
        df['end_time'] = pd.to_datetime(df['end_time'], format='mixed', errors='coerce')
        df = df.dropna(subset=['start_time'])

        df['date'] = df['start_time'].dt.date
        df['hour'] = df['start_time'].dt.hour
        df['category'] = df['category'].fillna("Sem Categoria")
        
        return df
    except Exception as e:
        st.error(f"Erro ao carregar banco de dados: {e}")
        return pd.DataFrame()

def format_duration_clean(seconds):
    if pd.isna(seconds):
        return "0m"
    h = int(seconds // 3600)
    m = int((seconds % 3600) // 60)
    if h > 0:
        return f"{h}h {m}m"
    return f"{m}m"

def clean_window_title(title):
    """Remove sufixos comuns de navegadores para limpar o gráfico."""
    if not title:
        return "Sem Título"
    
    # Lista de sufixos para remover e deixar apenas o nome do site/página
    suffixes_to_remove = [
        " - Opera",
        " - Google Chrome",
        " - Microsoft Edge",
        " - Mozilla Firefox",
        " - Brave",
        " - Vivaldi",
        " - YouTube"
    ]
    
    clean = str(title)
    for suffix in suffixes_to_remove:
        if suffix in clean:
            clean = clean.replace(suffix, "")
    
    return clean

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

def main():
    st.title("📊 Painel de Produtividade Pessoal")
    
    if 'limit_apps' not in st.session_state:
        st.session_state['limit_apps'] = 5

    tracker = ProductivityTracker()

    df_raw = load_data(tracker)

    if df_raw.empty:
        st.warning("Nenhum dado encontrado. Certifique-se de que o 'tracker.py' está rodando.")
        st.stop()
        return

    # --- Sidebar: Filtros ---
    st.sidebar.header("Filtros")
    
    available_dates = sorted(df_raw['date'].unique(), reverse=True)
    
    if not available_dates:
        st.sidebar.write("Sem datas disponíveis.")
        st.stop()

    selected_date, has_data = render_date_filter(available_dates)

    if has_data:
        df = df_raw[df_raw['date'] == selected_date].copy()
    else:
        df = df_raw.iloc[0:0].copy()
    
    # --- Mapa de Cores ---
    color_map = {}
    if 'hex_color' in df.columns:
        settings_df = df[['display_name', 'hex_color']].drop_duplicates().dropna()
        for _, row in settings_df.iterrows():
            if row['hex_color']:
                color_map[row['display_name']] = row['hex_color']

    if st.sidebar.button("Atualizar Dados"):
        st.rerun()

    # =========================================================================
    # ÁREA DE ABAS (Tabs)
    # =========================================================================
    
    tab_overview, tab_details, tab_customize = st.tabs([
        "🏠 Visão Geral",
        "🔍 Detalhes por App (Abas)",
        "⚙️ Personalizar Apps",
    ])

    # --- ABA 1: Visão Geral (Seu Dashboard Original) ---
    with tab_overview:
        # Métricas
        total_seconds = df['duration_seconds'].sum()
        hours = int(total_seconds // 3600)
        minutes = int((total_seconds % 3600) // 60)
        
        col1, col2, col3 = st.columns(3)
        with col1:
            st.metric("Tempo Total", f"{hours}h {minutes}m")
        with col2:
            st.metric("Sessões (Focos)", len(df))
        with col3:
            usage_by_app = df.groupby('display_name')['duration_seconds'].sum().sort_values(ascending=False)
            if not usage_by_app.empty:
                st.metric("App Mais Usado", usage_by_app.index[0])

        st.markdown("---")

        # LAYOUT 2x2
        row1_col1, row1_col2 = st.columns(2)

        # 1. Gráfico de Pizza
        with row1_col1:
            st.subheader("Distribuição (Top 5)")
            app_usage_s = df.groupby('display_name')['duration_seconds'].sum().sort_values(ascending=False).head(5)
            
            if not app_usage_s.empty:
                app_usage_df = app_usage_s.reset_index()
                app_usage_df.columns = ['display_name', 'duration_seconds']
                app_usage_df['formatted_time'] = app_usage_df['duration_seconds'].apply(format_duration_clean)
                
                fig_donut = px.pie(
                    app_usage_df, 
                    values='duration_seconds', 
                    names='display_name', 
                    hole=0.4,
                    color='display_name',
                    color_discrete_map=color_map,
                    color_discrete_sequence=px.colors.qualitative.Alphabet,
                    custom_data=['formatted_time']
                )
                fig_donut.update_traces(
                    textinfo='percent+label',
                    hovertemplate="<b>%{label}</b><br>⏱️ %{customdata[0]}<br>📊 %{percent}"
                )
                st.plotly_chart(fig_donut, use_container_width=True)
            else:
                st.info("Sem dados.")

        # 2. Gráfico de Barras (Linha do Tempo)
        with row1_col2:
            st.subheader("Linha do Tempo")
            hourly_usage = df.groupby(['hour', 'display_name'])['duration_seconds'].sum().reset_index()
            hourly_usage['duration_minutes'] = hourly_usage['duration_seconds'] / 60
            hourly_usage['formatted_time'] = hourly_usage['duration_seconds'].apply(format_duration_clean)
            
            if not hourly_usage.empty:
                fig_bar = px.bar(
                    hourly_usage, 
                    x='hour', 
                    y='duration_minutes',
                    color='display_name',
                    labels={'hour': 'Hora', 'duration_minutes': 'Min', 'display_name': 'App'},
                    color_discrete_map=color_map,
                    color_discrete_sequence=px.colors.qualitative.Alphabet,
                    custom_data=['formatted_time']
                )
                fig_bar.update_xaxes(tickmode='linear', dtick=1, range=[-0.5, 23.5])
                fig_bar.update_traces(
                    hovertemplate="<b>%{data.name}</b><br>🕒 Hora: %{x}h<br>⏱️ Tempo: %{customdata[0]}<extra></extra>"
                )
                st.plotly_chart(fig_bar, use_container_width=True, key="grafico1")
            else:
                st.info("Sem atividades.")

        st.markdown("---")

        row2_col1, row2_col2 = st.columns(2)

        # 3. Gráfico Horizontal (Ranking)
        with row2_col1:
            st.subheader(f"Ranking Detalhado")
            app_usage_all = df.groupby('display_name')['duration_seconds'].sum().sort_values(ascending=False)
            top_apps_view = app_usage_all.head(st.session_state['limit_apps']).reset_index()
            top_apps_view['formatted_time'] = top_apps_view['duration_seconds'].apply(format_duration_clean)
            top_apps_view = top_apps_view.sort_values(by='duration_seconds', ascending=False)

            if not top_apps_view.empty:
                fig_bar_h = px.bar(
                    top_apps_view,
                    x='duration_seconds',
                    y='display_name',
                    orientation='h',
                    text='formatted_time',
                    color='display_name', 
                    color_discrete_map=color_map,
                    color_discrete_sequence=px.colors.qualitative.Alphabet
                )
                fig_bar_h.update_traces(
                    textposition='auto', 
                    cliponaxis=False,
                    hovertemplate="<b>%{y}</b><br>⏱️ %{text}<extra></extra>"
                )
                fig_bar_h.update_layout(showlegend=False)
                
                chart_height = 100 + (len(top_apps_view) * 40)
                fig_bar_h.update_layout(
                    xaxis_title=None, yaxis_title=None, height=chart_height,
                    margin=dict(l=0, r=0, t=10, b=0),
                    xaxis=dict(showticklabels=False, showgrid=False, zeroline=False),
                    yaxis=dict(showgrid=False)
                )
                st.plotly_chart(fig_bar_h, use_container_width=True)

                if len(app_usage_all) > st.session_state['limit_apps']:
                    if st.button("➕ Mostrar Mais 5", key="btn_more"):
                        st.session_state['limit_apps'] += 5
                        st.rerun()
            else:
                st.info("Sem dados.")

        # 4. Gráfico de Categorias
        with row2_col2:
            st.subheader("Categorias")
            if 'category' in df.columns:
                cat_usage_s = df.groupby('category')['duration_seconds'].sum().sort_values(ascending=False)
                
                if not cat_usage_s.empty:
                    cat_usage_df = cat_usage_s.reset_index()
                    cat_usage_df.columns = ['category', 'duration_seconds']
                    cat_usage_df['formatted_time'] = cat_usage_df['duration_seconds'].apply(format_duration_clean)
                    
                    fig_cat = px.pie(
                        cat_usage_df, 
                        values='duration_seconds', 
                        names='category',
                        custom_data=['formatted_time']
                    )
                    fig_cat.update_traces(
                        hovertemplate="<b>%{label}</b><br>⏱️ %{customdata[0]}<br>📊 %{percent}"
                    )
                    st.plotly_chart(fig_cat, use_container_width=True)
                else:
                    st.info("Sem dados de categoria.")
            else:
                st.empty()
        
        st.subheader("Linha do Tempo")
        hourly_usage = df.groupby(['hour', 'display_name'])['duration_seconds'].sum().reset_index()
        hourly_usage['duration_minutes'] = hourly_usage['duration_seconds'] / 60
        hourly_usage['formatted_time'] = hourly_usage['duration_seconds'].apply(format_duration_clean)
        
        if not hourly_usage.empty:
            fig_bar = px.bar(
                hourly_usage, 
                x='hour', 
                y='duration_minutes',
                color='display_name',
                labels={'hour': 'Hora', 'duration_minutes': 'Min', 'display_name': 'App'},
                color_discrete_map=color_map,
                color_discrete_sequence=px.colors.qualitative.Alphabet,
                custom_data=['formatted_time']
            )
            fig_bar.update_xaxes(tickmode='linear', dtick=1, range=[-0.5, 23.5])
            fig_bar.update_traces(
                hovertemplate="<b>%{data.name}</b><br>🕒 Hora: %{x}h<br>⏱️ Tempo: %{customdata[0]}<extra></extra>"
            )
            fig_bar.update_layout(
                height=500,  # aumenta ou diminui a altura
                margin=dict(l=0, r=0, t=30, b=0)
            )
            st.plotly_chart(fig_bar, use_container_width=True, key="grafico2")
        else:
            st.info("Sem atividades.")
        
        st.markdown("---")
        st.subheader("Histórico Detalhado")
        
        display_df = df[['start_time', 'end_time', 'display_name', 'window_title', 'duration_seconds', 'category']].copy()
        display_df['duration_str'] = display_df['duration_seconds'].apply(lambda x: f"{int(x//60)}m {int(x%60)}s")
        display_df = display_df.sort_values(by='start_time', ascending=False)
        
        st.dataframe(
            display_df[['start_time', 'display_name', 'category', 'window_title', 'duration_str']], 
            use_container_width=True,
            hide_index=True
        )

    # --- ABA 2: Detalhes por App (A SOLUÇÃO DO OPERA) ---
    with tab_details:
        st.header("🔎 O que você fez dentro de cada App?")
        st.caption("Selecione um aplicativo (como o Opera) para ver em quais abas ou arquivos você passou mais tempo.")

        # 1. Seletor de App
        apps_list = df.groupby('display_name')['duration_seconds'].sum().sort_values(ascending=False).index.tolist()
        
        # Tenta selecionar 'Opera' ou 'opera.exe' por padrão se existir
        default_index = 0
        for i, app in enumerate(apps_list):
            if "opera" in app.lower():
                default_index = i
                break
        
        selected_app_detail = st.selectbox("Selecione o Aplicativo:", apps_list, index=default_index)

        if selected_app_detail:
            # Filtrar dados só deste app
            df_app = df[df['display_name'] == selected_app_detail].copy()
            
            # Limpar títulos (Remover " - Opera", etc)
            df_app['clean_title'] = df_app['window_title'].apply(clean_window_title)
            
            # Agrupar por Título da Janela (Aba)
            title_usage = df_app.groupby('clean_title')['duration_seconds'].sum().sort_values(ascending=True).tail(15) # Top 15
            
            col_d1, col_d2 = st.columns([2, 1])
            
            with col_d1:
                st.subheader(f"Top Abas/Janelas em: {selected_app_detail}")
                if not title_usage.empty:
                    title_usage_df = title_usage.reset_index()
                    title_usage_df['formatted_time'] = title_usage_df['duration_seconds'].apply(format_duration_clean)
                    
                    fig_titles = px.bar(
                        title_usage_df,
                        x='duration_seconds',
                        y='clean_title',
                        orientation='h',
                        text='formatted_time',
                        color='duration_seconds', # Gradiente por tempo
                        color_continuous_scale='Blues'
                    )
                    fig_titles.update_layout(
                        yaxis_title=None, 
                        xaxis_title="Tempo Gasto",
                        showlegend=False,
                        height=500
                    )
                    fig_titles.update_traces(
                        textposition='auto',
                        hovertemplate="<b>%{y}</b><br>⏱️ %{text}<extra></extra>"
                    )
                    st.plotly_chart(fig_titles, use_container_width=True)
                else:
                    st.info("Sem dados detalhados.")

            with col_d2:
                st.subheader("Histórico Cronológico")
                history_df = df_app[['start_time', 'clean_title', 'duration_seconds']].sort_values(by='start_time', ascending=False)
                history_df['Hora'] = history_df['start_time'].dt.strftime('%H:%M')
                history_df['Duração'] = history_df['duration_seconds'].apply(format_duration_clean)
                
                st.dataframe(
                    history_df[['Hora', 'clean_title', 'Duração']],
                    use_container_width=True,
                    hide_index=True,
                    height=500
                )

    with tab_customize:
        settings_ui.render_settings_tab(tracker)

if __name__ == "__main__":
    main()