# TimeTracker - Monitor de Produtividade Pessoal

O **TimeTracker** é uma aplicação para Windows desenvolvida em Python que monitoriza automaticamente a janela ativa do computador, registando quanto tempo é gasto em cada aplicação e site. O projeto inclui um dashboard interativo para análise de dados, gestão de categorias e um diário de produtividade.

![](images/dashboard.png)

## 🚀 Funcionalidades

- **Rastreio Automático**: Monitoriza a janela ativa em segundo plano e regista o tempo de uso na base de dados SQLite local.
- **Dashboard Interativo**: Interface web construída com Streamlit que oferece:
- Métricas de tempo total e foco.
- Gráficos de distribuição (Pizza) e linha do tempo (Barras) usando Plotly.
- Ranking detalhado de aplicações mais usadas.
- Análise específica por abas (ex: detalhar tempo gasto em abas do Opera/Chrome).

- **Personalização**: Permite renomear aplicações, atribuir cores e definir categorias (ex: Trabalho, Estudo, Lazer).
- **Diário de Feitos**: Uma secção integrada para registar anotações diárias sobre o que foi realizado.
- **System Tray**: A aplicação corre minimizada na bandeja do sistema, permitindo abrir o dashboard ou encerrar o processo facilmente.
- **Inicialização Automática**: Cria um script `.vbs` oculto na pasta de startup do Windows para iniciar com o sistema.

## 🛠️ Requisitos

- **Sistema Operativo**: Windows (devido ao uso das bibliotecas `pywin32` para captura de janelas).
- **Python**: Versão 3.8 ou superior recomendada.

## 📦 Instalação

1. Clone o repositório ou descarregue os ficheiros.
2. Instale as dependências listadas no `requirements.txt`:

```bash
pip install -r requirements.txt
```

> **Nota**: As principais bibliotecas incluem `streamlit`, `pandas`, `pywin32`, `plotly`, `pystray` e `Pillow`.

## ▶️ Como Usar

Para iniciar a aplicação em modo de desenvolvimento:

```bash
python main.py
```

Isto irá:

1. Iniciar o processo de rastreio (`tracker.py`) em segundo plano.
2. Lançar o servidor Streamlit (`dashboard.py`).
3. Adicionar um ícone à bandeja do sistema (perto do relógio).
4. Registar um script `.vbs` oculto na pasta de startup do Windows (se ainda não existir).
5. O dashboard fica disponível em `http://localhost:8501` (abra pelo ícone na bandeja do sistema).

### Funcionalidades do Dashboard

- **Visão Geral**: Veja onde gastou o seu tempo hoje.
- **Filtros**: Selecione datas anteriores na barra lateral.
- **Definições**: Na barra lateral, expanda "Personalizar Apps" para mudar cores ou categorias.
- **Detalhes**: Na aba "Detalhes por App", selecione um navegador para ver em quais sites passou mais tempo.

## 📂 Estrutura do Projeto

- `main.py`: O orquestrador principal. Inicia o tracker, o dashboard e o ícone da bandeja.
- `tracker.py`: O "motor" que captura a janela ativa e grava no SQLite.
- `dashboard.py`: A interface visual construída em Streamlit.
- `settings_ui.py`: Módulo da interface para gerir configurações das apps.
- `app_config.py`: Leitura e gravação das configurações de apps em JSON.
- `app_settings.json`: Configurações personalizadas dos apps (nome, cor, categoria).
- `productivity.db`: Base de dados SQLite com registos de atividade (gerada automaticamente na primeira execução).

## 📝 Notas

- Ao fechar a aplicação pelo "X" do terminal, o processo pode continuar a correr na bandeja. Use a opção "Sair" no ícone da bandeja para encerrar completamente.
- A base de dados utiliza o modo WAL (Write-Ahead Logging) para melhor performance e concorrência.
