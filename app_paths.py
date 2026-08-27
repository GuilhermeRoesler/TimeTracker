"""Caminhos da aplicação (código-fonte ou executável PyInstaller)."""

import os
import sys


def get_app_dir() -> str:
    """Diretório gravável da app (exe ou pasta do projeto)."""
    if getattr(sys, "frozen", False):
        return os.path.dirname(os.path.abspath(sys.executable))
    return os.path.dirname(os.path.abspath(__file__))


def get_resource_path(*parts: str) -> str:
    """Arquivo empacotado (_MEIPASS) ou ao lado do código-fonte."""
    if getattr(sys, "frozen", False):
        base = getattr(sys, "_MEIPASS", get_app_dir())
    else:
        base = get_app_dir()
    return os.path.join(base, *parts)
