import sys, types
from pathlib import Path
import pytest
root=Path('C:/Sewer-Studio_KI_4.5')
module=types.ModuleType('training')
module.__path__=[str(root/'training')]
sys.modules['training']=module
names=['test_osd_archiv_abdeckung_messung.py','test_osd_layout_review_bericht.py','test_osd_prefix_fallback_bericht.py']
args=[str(root/'training/scripts/tests'/n) for n in names]
args+=['-q','--basetemp='+str(root/'.tmp/programmaudit-2026-09-06/pytest-training-diagnostic'),'-o','cache_dir='+str(root/'.tmp/programmaudit-2026-09-06/pytest-cache-training-diagnostic'),'--junitxml='+str(root/'.tmp/programmaudit-2026-09-06/training-diagnostic.xml')]
raise SystemExit(pytest.main(args))
