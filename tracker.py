import sqlite3
import time
import os
import datetime
import logging
from typing import Optional, Tuple
import win32gui
import win32process
import win32api
import win32con

from app_config import get_app_settings, update_app_setting, load_config

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

            load_config()

            cursor.execute(
                "SELECT name FROM sqlite_master WHERE type='table' AND name='app_settings'"
            )
            if cursor.fetchone():
                cursor.execute("DROP TABLE app_settings")
                logging.info("Tabela app_settings removida (configurações migradas para JSON).")

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
        return get_app_settings()

    def update_app_setting(self, app_name, display_name, hex_color=None, category=None):
        """Atualiza configurações de um app."""
        return update_app_setting(app_name, display_name, hex_color, category)

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