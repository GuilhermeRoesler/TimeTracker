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

from tracker import ProductivityTracker

DASHBOARD_PORT = 8501
DASHBOARD_HOST = "localhost"
DASHBOARD_URL = f"http://{DASHBOARD_HOST}:{DASHBOARD_PORT}"
APP_NAME = "TimeTracker Pro"


def get_app_dir():
    return os.path.dirname(os.path.abspath(__file__))


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

    def create_startup_bat(self):
        """Cria script .bat na pasta de inicialização do Windows."""
        try:
            startup_folder = os.path.join(
                os.getenv("APPDATA"),
                r"Microsoft\Windows\Start Menu\Programs\Startup",
            )
            bat_path = os.path.join(startup_folder, f"{APP_NAME}.bat")
            app_dir = get_app_dir()
            main_script = os.path.join(app_dir, "main.py")
            python_exe = sys.executable

            bat_content = (
                "@echo off\r\n"
                f'cd /d "{app_dir}"\r\n'
                f'"{python_exe}" "{main_script}"\r\n'
            )

            needs_write = True
            if os.path.exists(bat_path):
                with open(bat_path, "r", encoding="utf-8") as f:
                    needs_write = f.read() != bat_content
            if needs_write:
                with open(bat_path, "w", encoding="utf-8") as f:
                    f.write(bat_content)

            shortcut_path = os.path.join(startup_folder, f"{APP_NAME}.lnk")
            if os.path.exists(shortcut_path):
                os.remove(shortcut_path)
        except Exception as e:
            print(f"Erro ao criar script de inicialização: {e}")

    def run_tracker(self):
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
        app_dir = get_app_dir()
        dashboard_script = os.path.join(app_dir, "dashboard.py")

        cmd = [
            sys.executable,
            "-m",
            "streamlit",
            "run",
            dashboard_script,
            "--server.port",
            str(DASHBOARD_PORT),
            "--server.headless",
            "true",
        ]

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
        self.create_startup_bat()

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
    app = AppOrchestrator()
    app.start()
