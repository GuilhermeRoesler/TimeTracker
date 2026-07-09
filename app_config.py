import json
import logging
import os
import sqlite3
from typing import Any, Dict, Optional

CONFIG_FILE = "app_settings.json"
DB_NAME = "productivity.db"

DEFAULT_CONFIG: Dict[str, Any] = {"apps": {}}


def _get_config_path() -> str:
    return os.path.join(os.path.dirname(os.path.abspath(__file__)), CONFIG_FILE)


def _normalize_config(data: Any) -> Dict[str, Any]:
    """Aceita formato legado (apps no root) e normaliza para {apps: {...}}."""
    if not isinstance(data, dict):
        return dict(DEFAULT_CONFIG)

    if "apps" in data and isinstance(data["apps"], dict):
        return {"apps": data["apps"]}

    # Formato legado: chaves de executáveis diretamente no root
    if data and all(isinstance(v, dict) for v in data.values()):
        return {"apps": data}

    return dict(DEFAULT_CONFIG)


def _load_raw_config() -> Dict[str, Any]:
    path = _get_config_path()
    if not os.path.exists(path):
        return dict(DEFAULT_CONFIG)

    try:
        with open(path, "r", encoding="utf-8") as f:
            return _normalize_config(json.load(f))
    except (json.JSONDecodeError, OSError) as e:
        logging.error(f"Erro ao ler {CONFIG_FILE}: {e}")
        return dict(DEFAULT_CONFIG)


def _save_config(config: Dict[str, Any]) -> bool:
    path = _get_config_path()
    try:
        with open(path, "w", encoding="utf-8") as f:
            json.dump(config, f, indent=2, ensure_ascii=False)
            f.write("\n")
        return True
    except OSError as e:
        logging.error(f"Erro ao salvar {CONFIG_FILE}: {e}")
        return False


def _migrate_from_sqlite(config: Dict[str, Any]) -> Dict[str, Any]:
    """Importa app_settings do SQLite na primeira execução, se o JSON estiver vazio."""
    if config.get("apps"):
        return config

    db_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), DB_NAME)
    if not os.path.exists(db_path):
        return config

    try:
        conn = sqlite3.connect(db_path)
        cursor = conn.cursor()
        cursor.execute(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='app_settings'"
        )
        if not cursor.fetchone():
            conn.close()
            return config

        cursor.execute(
            "SELECT app_name, display_name, hex_color, category FROM app_settings"
        )
        rows = cursor.fetchall()
        conn.close()

        if not rows:
            return config

        config["apps"] = {
            row[0]: {
                "display_name": row[1],
                "hex_color": row[2],
                "category": row[3],
            }
            for row in rows
        }
        _save_config(config)
        logging.info(f"Migração de app_settings do SQLite para {CONFIG_FILE} concluída.")
    except sqlite3.Error as e:
        logging.error(f"Erro ao migrar app_settings do SQLite: {e}")

    return config


def load_config() -> Dict[str, Any]:
    config = _load_raw_config()
    config = _migrate_from_sqlite(config)
    return config


def get_app_settings() -> Dict[str, Dict[str, Optional[str]]]:
    return load_config().get("apps", {})


def update_app_setting(
    app_name: str,
    display_name: str,
    hex_color: Optional[str] = None,
    category: Optional[str] = None,
) -> bool:
    config = load_config()
    config.setdefault("apps", {})
    config["apps"][app_name] = {
        "display_name": display_name,
        "hex_color": hex_color,
        "category": category,
    }
    return _save_config(config)
