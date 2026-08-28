import plotly.express as px

from dashboard.utils import format_duration_clean


def create_top_apps_donut(df, color_map):
    app_usage_s = (
        df.groupby("display_name")["duration_seconds"]
        .sum()
        .sort_values(ascending=False)
        .head(5)
    )
    if app_usage_s.empty:
        return None

    app_usage_df = app_usage_s.reset_index()
    app_usage_df.columns = ["display_name", "duration_seconds"]
    app_usage_df["formatted_time"] = app_usage_df["duration_seconds"].apply(format_duration_clean)

    fig = px.pie(
        app_usage_df,
        values="duration_seconds",
        names="display_name",
        hole=0.4,
        color="display_name",
        color_discrete_map=color_map,
        color_discrete_sequence=px.colors.qualitative.Alphabet,
        custom_data=["formatted_time"],
    )
    fig.update_traces(
        textinfo="percent+label",
        hovertemplate="<b>%{label}</b><br>⏱️ %{customdata[0]}<br>📊 %{percent}",
    )
    return fig


def create_hourly_timeline(df, color_map, height=None):
    hourly_usage = df.groupby(["hour", "display_name"])["duration_seconds"].sum().reset_index()
    hourly_usage["duration_minutes"] = hourly_usage["duration_seconds"] / 60
    hourly_usage["formatted_time"] = hourly_usage["duration_seconds"].apply(format_duration_clean)

    if hourly_usage.empty:
        return None

    fig = px.bar(
        hourly_usage,
        x="hour",
        y="duration_minutes",
        color="display_name",
        labels={"hour": "Hora", "duration_minutes": "Min", "display_name": "App"},
        color_discrete_map=color_map,
        color_discrete_sequence=px.colors.qualitative.Alphabet,
        custom_data=["formatted_time"],
    )
    fig.update_xaxes(tickmode="linear", dtick=1, range=[-0.5, 23.5])
    fig.update_traces(
        width=0.8,
        hovertemplate="<b>%{data.name}</b><br>🕒 Hora: %{x}h<br>⏱️ Tempo: %{customdata[0]}<extra></extra>",
    )

    layout_kwargs = {"margin": dict(l=0, r=0, t=30 if height else 10, b=0)}
    if height:
        layout_kwargs["height"] = height
    fig.update_layout(**layout_kwargs)
    return fig


def create_app_ranking(df, color_map, limit):
    app_usage_all = df.groupby("display_name")["duration_seconds"].sum().sort_values(ascending=False)
    top_apps_view = app_usage_all.head(limit).reset_index()
    top_apps_view["formatted_time"] = top_apps_view["duration_seconds"].apply(format_duration_clean)
    top_apps_view = top_apps_view.sort_values(by="duration_seconds", ascending=False)

    if top_apps_view.empty:
        return None, app_usage_all

    fig = px.bar(
        top_apps_view,
        x="duration_seconds",
        y="display_name",
        orientation="h",
        text="formatted_time",
        color="display_name",
        color_discrete_map=color_map,
        color_discrete_sequence=px.colors.qualitative.Alphabet,
    )
    fig.update_traces(
        textposition="auto",
        cliponaxis=False,
        hovertemplate="<b>%{y}</b><br>⏱️ %{text}<extra></extra>",
    )
    chart_height = 100 + (len(top_apps_view) * 40)
    fig.update_layout(
        showlegend=False,
        xaxis_title=None,
        yaxis_title=None,
        height=chart_height,
        margin=dict(l=0, r=0, t=10, b=0),
        xaxis=dict(showticklabels=False, showgrid=False, zeroline=False),
        yaxis=dict(showgrid=False),
    )
    return fig, app_usage_all


def create_category_pie(df):
    if "category" not in df.columns:
        return None

    cat_usage_s = df.groupby("category")["duration_seconds"].sum().sort_values(ascending=False)
    if cat_usage_s.empty:
        return None

    cat_usage_df = cat_usage_s.reset_index()
    cat_usage_df.columns = ["category", "duration_seconds"]
    cat_usage_df["formatted_time"] = cat_usage_df["duration_seconds"].apply(format_duration_clean)

    fig = px.pie(
        cat_usage_df,
        values="duration_seconds",
        names="category",
        custom_data=["formatted_time"],
    )
    fig.update_traces(
        hovertemplate="<b>%{label}</b><br>⏱️ %{customdata[0]}<br>📊 %{percent}",
    )
    return fig


def create_window_titles_chart(title_usage_df):
    if title_usage_df.empty:
        return None

    fig = px.bar(
        title_usage_df,
        x="duration_seconds",
        y="clean_title",
        orientation="h",
        text="formatted_time",
        color="duration_seconds",
        color_continuous_scale="Blues",
    )
    fig.update_layout(
        yaxis_title=None,
        xaxis_title="Tempo Gasto",
        showlegend=False,
        height=500,
    )
    fig.update_traces(
        textposition="auto",
        hovertemplate="<b>%{y}</b><br>⏱️ %{text}<extra></extra>",
    )
    return fig
