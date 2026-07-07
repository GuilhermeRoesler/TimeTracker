import sys
import os
import threading
import subprocess
import time
import webbrowser
import shutil
import win32com.client
import win32api
import win32con
from PIL import Image, ImageDraw
import pystray
from pystray import MenuItem as item

# Importar o tracker
from tracker import ProductivityTracker

# Configurações
DASHBOARD_PORT = 8501
DASHBOARD_HOST = "localhost"
DASHBOARD_URL = f"http://{DASHBOARD_HOST}:{DASHBOARD_PORT}"
APP_NAME = "TimeTracker Pro"

def get_resource_path(relative_path):
    """
    Retorna o caminho absoluto do recurso.
    Compatível com PyInstaller OneFile (Temp) e OneDir (Pasta Fixa).
    """
    if getattr(sys, 'frozen', False):
        # Se estamos rodando como executável (Frozen)
        if hasattr(sys, '_MEIPASS'):
            # Modo OneFile: usa a pasta temporária
            base_path = sys._MEIPASS
        else:
            # Modo OneDir: PyInstaller 6+ coloca dados em _internal/
            exe_dir = os.path.dirname(sys.executable)
            internal_dir = os.path.join(exe_dir, '_internal')
            base_path = internal_dir if os.path.isdir(internal_dir) else exe_dir
    else:
        # Modo Desenvolvimento
        base_path = os.path.dirname(os.path.abspath(__file__))
    
    return os.path.join(base_path, relative_path)

def is_pyinstaller_onefile():
    """Distingue OneFile de OneDir no PyInstaller 6+ (ambos definem _MEIPASS)."""
    if not getattr(sys, 'frozen', False):
        return False
    internal_dir = os.path.join(os.path.dirname(sys.executable), '_internal')
    return not os.path.isdir(internal_dir)

def get_app_dir():
    return os.path.dirname(get_resource_path("dashboard.py"))

class AppOrchestrator:
    def __init__(self):
        self.tracker_stop_event = threading.Event()
        self.streamlit_process = None
        self.icon = None
        self.tracker_thread = None
        
        # Registrar handler para interceptar o desligamento do Windows
        try:
            win32api.SetConsoleCtrlHandler(self._on_shutdown, True)
        except Exception as e:
            print(f"Erro ao registrar handler de shutdown: {e}")

    def _on_shutdown(self, sig):
        """Captura sinais de desligamento (Logoff/Shutdown) para limpeza segura."""
        if sig in [win32con.CTRL_SHUTDOWN_EVENT, win32con.CTRL_LOGOFF_EVENT, win32con.CTRL_CLOSE_EVENT]:
            # Executa limpeza
            self.cleanup()
            time.sleep(1)
            return True # Indica ao Windows que estamos tratando o evento
        return False

    def cleanup(self):
        """Centraliza a lógica de encerramento."""
        # 1. Sinaliza para o Tracker parar
        self.tracker_stop_event.set()
        
        # 2. Para o ícone da bandeja
        if self.icon:
            try:
                self.icon.stop()
            except Exception:
                pass

        # 3. Mata o processo do Streamlit
        if self.streamlit_process:
            try:
                self.streamlit_process.terminate()
                try:
                    self.streamlit_process.wait(timeout=2)
                except subprocess.TimeoutExpired:
                    self.streamlit_process.kill()
            except Exception:
                try:
                    self.streamlit_process.kill()
                except:
                    pass

    def create_startup_shortcut(self):
        """Cria atalho na pasta de inicialização do Windows."""
        try:
            startup_folder = os.path.join(os.getenv('APPDATA'), 
                                          r'Microsoft\Windows\Start Menu\Programs\Startup')
            shortcut_path = os.path.join(startup_folder, f'{APP_NAME}.lnk')
            
            if getattr(sys, 'frozen', False):
                target_path = sys.executable
                if not os.path.exists(shortcut_path):
                    shell = win32com.client.Dispatch("WScript.Shell")
                    shortcut = shell.CreateShortCut(shortcut_path)
                    shortcut.TargetPath = target_path
                    shortcut.WorkingDirectory = os.path.dirname(target_path)
                    shortcut.IconLocation = target_path
                    shortcut.save()
        except Exception as e:
            print(f"Erro ao criar atalho de inicialização: {e}")

    def run_tracker(self):
        """Roda o loop do tracker."""
        tracker = ProductivityTracker()
        tracker.start_time = time.time()
        last_app = None
        last_title = None

        while not self.tracker_stop_event.is_set():
            try:
                current_app, current_title = tracker.get_active_window_info()
                
                if current_app != last_app or current_title != last_title:
                    end_time = time.time()
                    if last_app is not None:
                        tracker.save_activity(last_app, last_title, tracker.start_time, end_time)
                    
                    tracker.start_time = end_time
                    last_app = current_app
                    last_title = current_title
                
                # Loop responsivo para saída rápida
                for _ in range(50):
                    if self.tracker_stop_event.is_set():
                        break
                    time.sleep(0.1)
                    
            except Exception as e:
                print(f"Erro no tracker: {e}")
                time.sleep(5)

        if last_app:
            tracker.save_activity(last_app, last_title, tracker.start_time, time.time())

    def run_streamlit(self):
        """Prepara e executa o Streamlit."""
        app_dir = get_app_dir()
        dashboard_script = get_resource_path("dashboard.py")
        
        if getattr(sys, 'frozen', False):
            # MODO EXECUTÁVEL
            cmd = [sys.executable, "--dashboard"]
        else:
            # MODO DESENVOLVIMENTO
            cmd = [sys.executable, "-m", "streamlit", "run", dashboard_script, 
                   "--server.port", str(DASHBOARD_PORT), 
                   "--server.headless", "true"]

        # Esconder janela do console
        kwargs = {}
        if os.name == 'nt':
            kwargs["creationflags"] = subprocess.CREATE_NO_WINDOW

        # cwd=app_dir garante que o Streamlit encontre dashboard.py e productivity.db
        # mesmo quando o app é iniciado fora da pasta do projeto (atalho, startup, etc.)
        self.streamlit_process = subprocess.Popen(
            cmd, cwd=app_dir, close_fds=True, **kwargs
        )

    def create_image(self):
        width = 64
        height = 64
        color1 = (0, 128, 255)
        color2 = (255, 255, 255)
        image = Image.new('RGB', (width, height), color1)
        dc = ImageDraw.Draw(image)
        dc.rectangle((width // 2, 0, width, height // 2), fill=color2)
        dc.rectangle((0, height // 2, width // 2, height), fill=color2)
        return image

    def open_dashboard(self, icon, item):
        webbrowser.open(DASHBOARD_URL)

    def quit_app(self, icon, item):
        self.cleanup()
        if self.icon:
            self.icon.stop()
        sys.exit(0)

    def start(self):
        self.create_startup_shortcut()
        
        self.tracker_thread = threading.Thread(target=self.run_tracker, daemon=True)
        self.tracker_thread.start()

        self.run_streamlit()

        image = self.create_image()
        menu = (
            item('Abrir Dashboard', self.open_dashboard, default=True),
            item('Sair', self.quit_app)
        )
        self.icon = pystray.Icon("TimeTracker", image, "Time Tracker", menu)
        self.icon.run()

if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "--dashboard":
        try:
            from streamlit.web import cli as stcli
            
            dashboard_path = get_resource_path("dashboard.py")
            
            # OneFile: copia scripts para TEMP (Streamlit não lê bem do _MEIPASS).
            # OneDir: roda direto de _internal/ para manter imports (tracker, settings_ui).
            if is_pyinstaller_onefile():
                temp_dir = os.path.join(os.getenv('TEMP'), "timetracker_app")
                os.makedirs(temp_dir, exist_ok=True)
                for script in ("dashboard.py", "tracker.py", "settings_ui.py"):
                    shutil.copy2(get_resource_path(script), os.path.join(temp_dir, script))
                dashboard_path = os.path.join(temp_dir, "dashboard.py")

            sys.argv = [
                "streamlit",
                "run",
                dashboard_path,
                "--server.port", str(DASHBOARD_PORT),
                "--server.headless", "true",
                "--global.developmentMode", "false"
            ]
            sys.exit(stcli.main())
        except Exception as e:
            import traceback
            log_path = os.path.join(get_app_dir(), "streamlit_error.log")
            with open(log_path, "w", encoding="utf-8") as f:
                f.write(traceback.format_exc())
            sys.exit(1)

    app = AppOrchestrator()
    app.start()