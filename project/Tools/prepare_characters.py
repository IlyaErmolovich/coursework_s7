"""Deterministic extraction from the user's original PNGs; requires Pillow, numpy, scipy.
Only alpha outside the coherent silhouettes is cleaned. Source RGB within parts is preserved.
Forearms and mittens are separated at their fabric color boundary, then rotated losslessly.
Run from any directory. Originals and reference poses are never overwritten.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi
ROOT=Path(__file__).resolve().parents[1]
REGIONS={
 'red':{'Head':(145,20,560,445),'Torso':(580,207,1000,500),'Scarf':(1005,210,1430,463),
 'UpperArmL':(148,460,310,644),'ArmL':(340,490,615,643),'ArmR':(930,485,1200,640),'UpperArmR':(1232,461,1390,652),
 'ThighL':(540,637,688,793),'ThighR':(860,630,1002,793),'BootL':(470,788,664,982),'BootR':(875,788,1068,982)},
 'teal':{'Head':(110,30,515,445),'Torso':(552,214,1018,548),'Scarf':(1005,211,1468,475),
 'UpperArmL':(94,474,277,685),'ArmL':(303,515,612,676),'ArmR':(936,513,1242,676),'UpperArmR':(1270,474,1452,685),
 'ThighL':(590,648,751,821),'ThighR':(825,648,984,821),'BootL':(505,821,724,1007),'BootR':(855,819,1073,1007)}}
# Keep only the coat core; fixed sleeves/collar are replaced by articulated components.
TORSO={'red':[(716,316),(894,316),(916,381),(942,470),(902,493),(701,491),(678,463),(700,375)],
       'teal':[(713,353),(898,355),(928,416),(949,510),(908,538),(678,527),(650,501),(688,400)]}
MANIFEST=[]
def largest(mask):
 lab,n=ndi.label(mask)
 if not n:raise ValueError('Empty sprite')
 sizes=np.bincount(lab.ravel());sizes[0]=0
 return lab==sizes.argmax()
def emit(variant,name,arr,crop_bounds=None):
 # Final isolated components cannot contain floating pixels, including after mitten splitting.
 coherent=largest(ndi.binary_opening(arr[:,:,3]>128,iterations=1))
 coherent=ndi.binary_fill_holes(coherent)
 alpha=np.minimum(arr[:,:,3],np.round(ndi.gaussian_filter(coherent.astype(float),.55)*255).astype(np.uint8))
 alpha[alpha<8]=0
 arr=arr.copy();arr[:,:,3]=alpha;arr[alpha==0,:3]=0
 yy,xx=np.where(alpha>0)
 if crop_bounds is None:crop_bounds=(yy.min(),yy.max()+1,xx.min(),xx.max()+1)
 ya,yb,xa,xb=crop_bounds;arr=arr[ya:yb,xa:xb]
 result=Image.new('RGBA',(arr.shape[1]+16,arr.shape[0]+16));result.paste(Image.fromarray(arr),(8,8))
 out=ROOT/'Assets/Art/Characters'/variant;out.mkdir(parents=True,exist_ok=True)
 result.save(out/(name+'.png'))
 # Pixel coordinates are bottom-left in Unity; pivot resides in the overlapping joint cap.
 pivot={'Head':(.50,.06),'Torso':(.50,.24),'Scarf':(.66,.59),'ScarfFront':(.66,.59),'PocketL':(.5,.5),'PocketR':(.5,.5),'UpperArmL':(.70,.88),'UpperArmR':(.30,.88),'ThighL':(.59,.88),'ThighR':(.41,.88),'BootL':(.68,.88),'BootR':(.32,.88)}.get(name,(.50,.88))
 MANIFEST.append({'variant':variant,'name':name,'width':result.width,'height':result.height,'pivotX':pivot[0],'pivotY':pivot[1]})
 return crop_bounds
for variant,regions in REGIONS.items():
 src=np.array(Image.open(ROOT/'ArtSource/Characters'/(variant+'-parts.png')).convert('RGBA'))
 for name,(x0,y0,x1,y1) in regions.items():
  arr=src[y0:y1,x0:x1].copy()
  mask=largest(ndi.binary_opening(arr[:,:,3]>140,iterations=2))
  if name=='Torso':
   polygon=Image.new('L',(x1-x0,y1-y0));ImageDraw.Draw(polygon).polygon([(x-x0,y-y0) for x,y in TORSO[variant]],fill=255)
   mask&=np.array(polygon)>0
  arr[:,:,3]=np.minimum(arr[:,:,3],(mask*255).astype(np.uint8))
  if name.startswith('Arm'):
   rgb=arr[:,:,:3].astype(float)
   glove=(rgb[:,:,0]>rgb[:,:,1]*1.7) if variant=='red' else (rgb[:,:,1]<rgb[:,:,2]*.85)
   glove&=mask
   glove=largest(ndi.binary_opening(glove,iterations=2))
   # Boundary falls under the cuff in the assembled rig. A tiny overlap prevents a seam.
   for part,keep in [('Forearm',mask&~ndi.binary_erosion(glove,iterations=2)),('Mitten',ndi.binary_dilation(glove,iterations=2)&mask)]:
    a=arr.copy();a[:,:,3]*=keep.astype(np.uint8)
    a=np.rot90(a,3 if name.endswith('L') else 1).copy()
    emit(variant,part+name[-1],a)
  elif name=='Scarf':
   bounds=emit(variant,name,arr)
   poly=Image.new('L',(x1-x0,y1-y0))
   points=([(1147,227),(1235,246),(1392,240),(1423,265),(1423,335),(1378,365),(1228,358),(1140,325),(1129,282)] if variant=='red' else [(1158,228),(1233,246),(1429,231),(1463,259),(1461,333),(1418,367),(1233,357),(1141,326),(1127,281)])
   ImageDraw.Draw(poly).polygon([(x-x0,y-y0) for x,y in points],fill=255)
   arr[:,:,3]=np.minimum(arr[:,:,3],np.array(poly));emit(variant,'ScarfFront',arr,bounds)
  else:emit(variant,name,arr)
# Missing cream pockets are copied from the supplied full-height reference, not redrawn.
ref=np.array(Image.open(ROOT/'ArtSource/Characters/teal-reference.png').convert('RGBA'))
for name,box in [('PocketL',(460,870,566,981)),('PocketR',(681,867,755,966))]:
 x0,y0,x1,y1=box;arr=ref[y0:y1,x0:x1].copy();rgb=arr[:,:,:3]
 mask=(rgb[:,:,0]>205)&(rgb[:,:,1]>185)&(rgb[:,:,2]<230)&(arr[:,:,3]>128)
 arr[:,:,3]*=largest(mask).astype(np.uint8);emit('teal',name,arr)
(ROOT/'Assets/Art/Characters/parts.json').write_text(json.dumps({'parts':MANIFEST},indent=2)+'\n')
print('Extracted',len(MANIFEST),'sprites; all source images preserved.')
# Clean reference margins for side-by-side viewing only; the original files remain unchanged.
refdir=ROOT/'Assets/Resources/Characters/References';refdir.mkdir(parents=True,exist_ok=True)
for variant in REGIONS:
 arr=np.array(Image.open(ROOT/'ArtSource/Characters'/(variant+'-reference.png')).convert('RGBA'))
 mask=largest(ndi.binary_opening(arr[:,:,3]>140,iterations=2));arr[:,:,3]=np.minimum(arr[:,:,3],mask.astype(np.uint8)*255)
 yy,xx=np.where(mask);Image.fromarray(arr[yy.min():yy.max()+1,xx.min():xx.max()+1]).save(refdir/(variant+'.png'))
