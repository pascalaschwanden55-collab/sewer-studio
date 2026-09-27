from pathlib import Path
import sys, json, ast, types, contextlib, base64, io
from unittest.mock import patch
root=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(root/'sidecar'))
from sidecar.models import dino_wrapper as d
from PIL import Image

dep=root/'sidecar/.venv/Lib/site-packages/groundingdino/util/inference.py'
tree=ast.parse(dep.read_text(encoding='utf-8'))
function=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='predict')
actual_default=ast.literal_eval(function.args.defaults[-1])
results=[]
buf=io.BytesIO(); Image.new('RGB',(32,32),(80,80,80)).save(buf,format='PNG')
image=base64.b64encode(buf.getvalue()).decode()
for requested in ['cpu','cuda:1','cuda:0']:
 observed={}
 def predict(model,image,caption,box_threshold,text_threshold,device=actual_default):
  observed['predict_device']=device
  return [],[],[]
 class Manager:
  def busy_slot(self,*a):return contextlib.nullcontext()
  def ensure_loaded(self,slot,device,loader):
   observed['managed_device']=device
   return types.SimpleNamespace(model=object())
 transforms=types.SimpleNamespace(Compose=lambda seq:lambda x:x,ToTensor=lambda:None,Normalize=lambda *a:None)
 modules={'groundingdino':types.ModuleType('groundingdino'),'groundingdino.util':types.ModuleType('groundingdino.util'),
  'groundingdino.util.inference':types.SimpleNamespace(predict=predict),'torch':types.ModuleType('torch'),
  'torchvision':types.SimpleNamespace(transforms=transforms)}
 with patch.dict(sys.modules,modules),patch.object(d,'gpu_manager',Manager()),patch.object(d,'_resolve_device',return_value=requested):
  response=d.detect(image,'crack',.25,.2)
 results.append({'requested':requested,**observed,'degraded':response.degraded,'dependency_default':actual_default})
(Path(__file__).parent/'dino-results.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print(json.dumps(results,indent=2))
