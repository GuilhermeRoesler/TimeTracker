import sqlite3
import time
import os
import shutil
import datetime
import logging
from typing import Optional, Tuple
import win32gui
import win32process
import win32api
import win32con

# Configuração de Logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    datefmt='%Y-%m-%d %H:%M:%S'
)

DB_NAME = "productivity.db"

class ProductivityTracker:
    def __init__(self, db_path: str = DB_NAME):
        self.db_path = db_path
        self._init_db()
        self.current_window = None
        self.start_time = None

    def _init_db(self):
        """Inicializa o banco de dados e realiza migrações de esquema se necessário."""
        try:
            conn = sqlite3.connect(self.db_path)
            cursor = conn.cursor()
            
            # Habilita Write-Ahead Logging
            cursor.execute("PRAGMA journal_mode=WAL;")
            
            cursor.execute("""
                CREATE TABLE IF NOT EXISTS activity_log (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    app_name TEXT NOT NULL,
                    window_title TEXT,
                    start_time TIMESTAMP NOT NULL,
                    end_time TIMESTAMP,
                    duration_seconds REAL
                )
            """)
            
            # --- MIGRAÇÃO DE ESQUEMA (Remover icon_path, renomear pretty_name) ---
            # Verifica colunas existentes na tabela app_settings
            cursor.execute("PRAGMA table_info(app_settings)")
            columns_info = cursor.fetchall()
            column_names = [info[1] for info in columns_info]
            
            # Se a tabela existe e tem as colunas antigas, faz a migração
            if 'pretty_name' in column_names or 'icon_path' in column_names:
                logging.info("Iniciando migração de banco de dados (Schema Update)...")
                try:
                    # 1. Renomear tabela antiga
                    cursor.execute("ALTER TABLE app_settings RENAME TO app_settings_old")
                    
                    # 2. Criar nova tabela com esquema limpo
                    cursor.execute("""
                        CREATE TABLE app_settings (
                            app_name TEXT PRIMARY KEY,
                            display_name TEXT,
                            hex_color TEXT,
                            category TEXT
                        )
                    """)
                    
                    # 3. Copiar dados (Mapeando pretty_name -> display_name)
                    # Verifica se hex_color e category existiam na antiga para evitar erros no SELECT
                    cols_old = ", ".join(column_names)
                    has_color = 'hex_color' in column_names
                    has_cat = 'category' in column_names
                    
                    # Monta query de migração segura
                    sel_color = "hex_color" if has_color else "NULL"
                    sel_cat = "category" if has_cat else "'Sem Categoria'"
                    
                    cursor.execute(f"""
                        INSERT INTO app_settings (app_name, display_name, hex_color, category)
                        SELECT app_name, pretty_name, {sel_color}, {sel_cat}
                        FROM app_settings_old
                    """)
                    
                    # 4. Apagar tabela velha
                    cursor.execute("DROP TABLE app_settings_old")
                    conn.commit()
                    logging.info("Migração concluída com sucesso!")
                    
                except sqlite3.Error as e:
                    logging.error(f"Erro durante a migração: {e}")
                    conn.rollback()
            else:
                # Criação padrão se não existir ou se já estiver no novo formato
                cursor.execute("""
                    CREATE TABLE IF NOT EXISTS app_settings (
                        app_name TEXT PRIMARY KEY,
                        display_name TEXT,
                        hex_color TEXT,
                        category TEXT
                    )
                """)

            conn.commit()
            conn.close()
            logging.info("Banco de dados inicializado com sucesso.")
        except sqlite3.Error as e:
            logging.error(f"Erro ao inicializar banco de dados: {e}")

    def get_active_window_info(self) -> Tuple[Optional[str], Optional[str]]:
        """Captura o nome do executável e o título da janela ativa."""
        try:
            hwnd = win32gui.GetForegroundWindow()
            if not hwnd:
                return None, None

            _, pid = win32process.GetWindowThreadProcessId(hwnd)
            
            try:
                handle = win32api.OpenProcess(
                    win32con.PROCESS_QUERY_INFORMATION | win32con.PROCESS_VM_READ,
                    False, 
                    pid
                )
                exe_path = win32process.GetModuleFileNameEx(handle, 0)
                app_name = os.path.basename(exe_path)
                win32api.CloseHandle(handle)
            except Exception:
                app_name = "System/Protected"

            window_title = win32gui.GetWindowText(hwnd)
            return app_name, window_title

        except Exception as e:
            logging.error(f"Erro ao capturar janela: {e}")
            return None, None

    def save_activity(self, app_name: str, window_title: str, start: float, end: float):
        """Salva o registro de atividade no banco."""
        try:
            conn = sqlite3.connect(self.db_path)
            cursor = conn.cursor()
            
            current_start = start
            while current_start < end:
                dt_start = datetime.datetime.fromtimestamp(current_start)
                next_hour = (dt_start + datetime.timedelta(hours=1)).replace(minute=0, second=0, microsecond=0)
                ts_next_hour = next_hour.timestamp()
                
                current_end = min(end, ts_next_hour)
                duration = current_end - current_start
                
                if duration >= 1.0:
                    cursor.execute("""
                        INSERT INTO activity_log (app_name, window_title, start_time, end_time, duration_seconds)
                        VALUES (?, ?, ?, ?, ?)
                    """, (app_name, window_title, 
                          datetime.datetime.fromtimestamp(current_start), 
                          datetime.datetime.fromtimestamp(current_end), 
                          duration))
                
                current_start = current_end

            conn.commit()
            conn.close()
        except sqlite3.Error as e:
            logging.error(f"Erro ao salvar atividade: {e}")

    def get_all_apps(self):
        """Retorna lista de todos os apps registrados no log."""
        try:
            conn = sqlite3.connect(self.db_path)
            cursor = conn.cursor()
            cursor.execute("SELECT DISTINCT app_name FROM activity_log ORDER BY app_name")
            apps = [row[0] for row in cursor.fetchall()]
            conn.close()
            return apps
        except sqlite3.Error:
            return []

    def get_app_settings(self):
        """Retorna dicionário com configurações dos apps."""
        settings = {}
        try:
            conn = sqlite3.connect(self.db_path)
            cursor = conn.cursor()
            # Esquema novo: display_name, hex_color, category
            cursor.execute("SELECT app_name, display_name, hex_color, category FROM app_settings")
            for row in cursor.fetchall():
                settings[row[0]] = {
                    "display_name": row[1], 
                    "hex_color": row[2],
                    "category": row[3]
                }
            conn.close()
        except sqlite3.Error:
            pass
        return settings

    def update_app_setting(self, app_name, display_name, hex_color=None, category=None):
        """Atualiza configurações de um app (sem ícone)."""
        try:
            conn = sqlite3.connect(self.db_path)
            cursor = conn.cursor()
            cursor.execute("""
                INSERT OR REPLACE INTO app_settings (app_name, display_name, hex_color, category)
                VALUES (?, ?, ?, ?)
            """, (app_name, display_name, hex_color, category))
            conn.commit()
            conn.close()
            return True
        except sqlite3.Error as e:
            logging.error(f"Erro ao atualizar settings: {e}")
            return False

    def run(self):
        """Loop principal de monitoramento."""
        logging.info("Iniciando monitoramento...")
        self.start_time = time.time()
        last_app = None
        last_title = None
        
        try:
            while True:
                current_app, current_title = self.get_active_window_info()
                if current_app != last_app or current_title != last_title:
                    end_time = time.time()
                    if last_app is not None:
                        self.save_activity(last_app, last_title, self.start_time, end_time)
                    self.start_time = end_time
                    last_app = current_app
                    last_title = current_title
                time.sleep(5)
        except KeyboardInterrupt:
            pass

if __name__ == "__main__":
    tracker = ProductivityTracker()
    tracker.run()