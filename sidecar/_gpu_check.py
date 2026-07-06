import sys
try:
    import torch
except Exception as exc:
    print('  TORCH-IMPORT-FEHLER:', exc); sys.exit(2)
ok = torch.cuda.is_available()
name = torch.cuda.get_device_name(0) if ok else 'CPU'
cap = 'sm_' + ''.join(map(str, torch.cuda.get_device_capability(0))) if ok else '-'
print(f'  torch={torch.__version__}  cuda_build={torch.version.cuda}  available={ok}  device={name}  capability={cap}')
if not ok:
    print('  FEHLER: torch.cuda.is_available()=False - falscher Torch-/CUDA-Build fuer diese GPU?')
    sys.exit(3)
