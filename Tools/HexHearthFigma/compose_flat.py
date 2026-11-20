import json,sys
from PIL import Image, ImageDraw, ImageFont
OUT=sys.argv[1]
L=json.load(open(f'{OUT}/hexhearth_layout.json'))
FONTS={('Cormorant Garamond','Regular'):'cormorant-garamond/400Regular/CormorantGaramond_400Regular.ttf',('Cormorant Garamond','Medium'):'cormorant-garamond/500Medium/CormorantGaramond_500Medium.ttf',('Cormorant Garamond','SemiBold'):'cormorant-garamond/600SemiBold/CormorantGaramond_600SemiBold.ttf',('Inter','Medium'):'inter/500Medium/Inter_500Medium.ttf'}
def paste(img,fn,x,y,w,h,color=None,alpha=1.0):
    sp=Image.open(f'{OUT}/Sprites/{fn}').convert('RGBA').resize((max(1,round(w)),max(1,round(h))))
    if color:
        r,g,b,a=color; px=Image.new('RGBA',sp.size,(round(r*255),round(g*255),round(b*255),255)); px.putalpha(sp.getchannel('A').point(lambda v:v*a*alpha)); sp=px
    elif alpha<1: sp.putalpha(sp.getchannel('A').point(lambda v:v*alpha))
    img.alpha_composite(sp,(round(x),round(y)))
for s in L['screens']:
    img=Image.new('RGBA',(s['width'],s['height']),(0,0,0,255)); paste(img,s['backdrop'],0,0,s['width'],s['height'])
    N=s['nodes']; ax={}; al={}
    for n in N:
        px,py,pa=(0,0,1) if n['parent']<0 else (ax[n['parent']][0],ax[n['parent']][1],al[n['parent']])
        x,y=px+n['x'],py+n['y']; ax[n['id']]=(x,y); a=pa*(n['opacity'] if n['kind']=='frame' else 1); al[n['id']]=a
        if n['kind']=='image': paste(img,n['sprite'],x,y,n['w'],n['h'],n['color'] if n['hasColor'] else None,pa)
        elif n['kind']=='frame' and n['visualSprite']: paste(img,n['visualSprite'],x+n['visualX'],y+n['visualY'],n['visualW'],n['visualH'],n['visualColor'] if n['visualHasColor'] else None,a)
        elif n['kind']=='text':
            if n['glowSprite']: paste(img,n['glowSprite'],x+n['glowX'],y+n['glowY'],n['glowW'],n['glowH'],None,pa)
            f=ImageFont.truetype('node_modules/@expo-google-fonts/'+FONTS[(n['family'],n['style'])],round(n['fontSize']))
            c=n['gradStops'][1] if n['hasGradient'] else None
            col=(c['r'],c['g'],c['b'],c['a']) if c else tuple(n['textColor'])
            layer=Image.new('RGBA',img.size); d=ImageDraw.Draw(layer)
            sp=n['spacing']*n['fontSize']/100
            tw=sum(d.textlength(ch,font=f)+sp for ch in n['chars'])
            cx=x+(n['w']-tw)/2 if (n['autoWidth'] or n['align']=='CENTER') else x
            for ch in n['chars']:
                d.text((cx,y+n['baseline']),ch,font=f,anchor='ls',fill=tuple(round(v*255) for v in col[:3])+(round(col[3]*pa*255),)); cx+=d.textlength(ch,font=f)+sp
            img.alpha_composite(layer)
    img.convert('RGB').save(f"{OUT}/preview_{s['key']}.png")
