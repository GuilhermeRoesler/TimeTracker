import PyInstaller.__main__
import os
import shutil
import datetime

def backup_database():
    """
    Cria um backup do banco de dados antes de limpar as pastas.
    Verifica tanto na raiz quanto na pasta dist antiga.
    """
    db_name = "productivity.db"
    backup_dir = "backups"
    
    # Criar pasta de backups se não existir
    if not os.path.exists(backup_dir):
        os.makedirs(backup_dir)
        print(f"📂 Pasta '{backup_dir}' criada.")

    timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    
    # 1. Tentar fazer backup do DB local (Desenvolvimento)
    if os.path.exists(db_name):
        backup_name = f"productivity_DEV_{timestamp}.db"
        try:
            shutil.copy2(db_name, os.path.join(backup_dir, backup_name))
            print(f"✅ Backup do banco LOCAL criado: {backup_name}")
        except Exception as e:
            print(f"❌ Erro ao copiar banco local: {e}")

    # 2. Tentar fazer backup do DB dentro da dist (Produção/Exe anterior)
    #    Isso é crucial se você estava usando o .exe e salvando dados lá!
    dist_db_path = os.path.join("dist", "TimeTracker", db_name)
    if os.path.exists(dist_db_path):
        backup_name = f"productivity_DIST_{timestamp}.db"
        try:
            shutil.copy2(dist_db_path, os.path.join(backup_dir, backup_name))
            print(f"✅ Backup do banco DIST (Exe antigo) criado: {backup_name}")
        except Exception as e:
            print(f"❌ Erro ao copiar banco da dist: {e}")

def build_exe():
    print("🛡️  Iniciando rotina de segurança...")
    
    # Executar Backup ANTES de qualquer deleção
    backup_database()

    print("\n🧹 Limpar builds anteriores...")
    # Usar ignore_errors=True para evitar crash se arquivo estiver em uso, mas avisa
    if os.path.exists("build"): 
        try:
            shutil.rmtree("build")
        except Exception as e:
            print(f"⚠️  Aviso: Não foi possível apagar totalmente 'build': {e}")

    if os.path.exists("dist"): 
        try:
            shutil.rmtree("dist")
        except Exception as e:
            print(f"⚠️  Aviso: Não foi possível apagar totalmente 'dist': {e}")

    print("\n🔨 Iniciando PyInstaller...")

    # Definir os argumentos do PyInstaller
    args = [
        'main.py',                       # Script principal
        '--name=TimeTracker',            # Nome do EXE
        '--onedir',                      # Pasta ao invés de arquivo único
        '--noconsole',                   # Não mostrar console preto
        '--clean',
        
        # Incluir arquivos de dados (Source;Dest)
        '--add-data=dashboard.py;.',     
        '--add-data=tracker.py;.',       
        '--add-data=settings_ui.py;.',   
        
        # Imports ocultos
        '--hidden-import=streamlit',
        '--hidden-import=pandas',
        '--hidden-import=plotly',
        '--hidden-import=win32timezone',
        
        # Coleta de metadados
        '--collect-all=streamlit',
        '--collect-all=altair',
        '--collect-all=pandas',
        '--collect-all=pyarrow',
    ]

    # Executar PyInstaller
    PyInstaller.__main__.run(args)
    
    print("\n🎉 Build concluído! Verifique a pasta 'dist/TimeTracker'.")
    print(f"💾 Seus dados antigos estão salvos na pasta 'backups'.")

if __name__ == "__main__":
    build_exe()