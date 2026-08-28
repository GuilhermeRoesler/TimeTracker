import sys
import os
import threading
import subprocess
import time
import webbrowser
import win32api
import win32con
from PIL import Image, ImageDraw
import pystray
from pystray import MenuItem as item

from app_paths import get_app_dir, get_resource_path
from tracker import ProductivityTracker

DASHBOARD_PORT = 8501
DASHBOARD_HOST = "localhost"
DASHBOARD_URL = f"http://{DASHBOARD_HOST}:{DASHBOARD_PORT}"
APP_NAME = "TimeTracker Pro"
STREAMLIT_FLAG = "--timetracker-streamlit"


def get_pythonw_executable():
    python_dir = os.path.dirname(sys.executable)
    pythonw_exe = os.path.join(python_dir, "pythonw.exe")
    return pythonw_exe if os.path.exists(pythonw_exe) else sys.executable


def _run_as_streamlit_cli(argv: list) -> None:
    """Reentrada do exe empacotado para servir o dashboard Streamlit."""
    sys.argv = [sys.argv[0], "run", *argv]
    from streamlit.web import cli as stcli

    raise SystemExit(stcli.main())


class AppOrchestrator:
    def __init__(self):
        self.tracker_stop_event = threading.Event()
        self.streamlit_process = None
        self.icon = None
        self.tracker_thread = None

        try:
            win32api.SetConsoleCtrlHandler(self._on_shutdown, True)
        except Exception as e:
            print(f"Erro ao registrar handler de shutdown: {e}")

    def _on_shutdown(self, sig):
        if sig in [win32con.CTRL_SHUTDOWN_EVENT, win32con.CTRL_LOGOFF_EVENT, win32con.CTRL_CLOSE_EVENT]:
            self.cleanup()
            time.sleep(1)
            return True
        return False

    def cleanup(self):
        self.tracker_stop_event.set()

        if self.icon:
            try:
                self.icon.stop()
            except Exception:
                pass

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
                except Exception:
                    pass

    def create_startup_shortcut(self):
        """Cria atalho na pasta de inicialização do Windows."""
        try:
            import win32com.client

            startup_folder = os.path.join(
                os.getenv("APPDATA"),
                r"Microsoft\Windows\Start Menu\Programs\Startup",
            )
            shortcut_path = os.path.join(startup_folder, f"{APP_NAME}.lnk")
            app_dir = get_app_dir()
            if getattr(sys, "frozen", False):
                target = sys.executable
                arguments = ""
            else:
                target = get_pythonw_executable()
                arguments = f'"{os.path.join(app_dir, "main.py")}"'

            shell = win32com.client.Dispatch("WScript.Shell")
            needs_write = True
            if os.path.exists(shortcut_path):
                shortcut = shell.CreateShortCut(shortcut_path)
                needs_write = not (
                    os.path.normcase(shortcut.Targetpath) == os.path.normcase(target)
                    and shortcut.Arguments == arguments
                    and os.path.normcase(shortcut.WorkingDirectory) == os.path.normcase(app_dir)
                )

            if needs_write:
                shortcut = shell.CreateShortCut(shortcut_path)
                shortcut.Targetpath = target
                shortcut.Arguments = arguments
                shortcut.WorkingDirectory = app_dir
                shortcut.WindowStyle = 7
                shortcut.Description = APP_NAME
                shortcut.save()

            for legacy_name in (f"{APP_NAME}.vbs", f"{APP_NAME}.bat"):
                legacy_path = os.path.join(startup_folder, legacy_name)
                if os.path.exists(legacy_path):
                    os.remove(legacy_path)
        except Exception as e:
            print(f"Erro ao criar atalho de inicialização: {e}")

    def run_tracker(self):
        tracker = ProductivityTracker()
        tracker.run(stop_event=self.tracker_stop_event)

    def run_streamlit(self):
        app_dir = get_app_dir()
        dashboard_script = get_resource_path("dashboard", "app.py")
        streamlit_args = [
            dashboard_script,
            "--server.port",
            str(DASHBOARD_PORT),
            "--server.headless",
            "true",
            "--global.developmentMode",
            "false",
        ]

        if getattr(sys, "frozen", False):
            cmd = [sys.executable, STREAMLIT_FLAG, *streamlit_args]
        else:
            cmd = [sys.executable, "-m", "streamlit", "run", *streamlit_args]

        kwargs = {}
        if os.name == "nt":
            kwargs["creationflags"] = subprocess.CREATE_NO_WINDOW

        self.streamlit_process = subprocess.Popen(
            cmd, cwd=app_dir, close_fds=True, **kwargs
        )

    def create_image(self):
        width = 64
        height = 64
        color1 = (0, 128, 255)
        color2 = (255, 255, 255)
        image = Image.new("RGB", (width, height), color1)
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
            item("Abrir Dashboard", self.open_dashboard, default=True),
            item("Sair", self.quit_app),
        )
        self.icon = pystray.Icon("TimeTracker", image, "Time Tracker", menu)
        self.icon.run()


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == STREAMLIT_FLAG:
        _run_as_streamlit_cli(sys.argv[2:])
    else:
        app = AppOrchestrator()
        app.start()
