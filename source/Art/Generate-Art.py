from PIL import Image, ImageDraw
from pathlib import Path
out = Path(__file__).resolve().parents[2] / 'mod' / 'Textures' / 'Paper'
out.mkdir(parents=True,exist_ok=True)
S=3
def bracelet(worn):
    im=Image.new('RGBA',(600*S,400*S),'#152332'); d=ImageDraw.Draw(im)
    def ellipse(box,fill=None,outline=None,width=1): d.ellipse(tuple(int(v*S) for v in box),fill=fill,outline=outline,width=width*S)
    def line(points,fill,width): d.line([(int(x*S),int(y*S)) for x,y in points],fill=fill,width=width*S)
    def rect(box,fill): d.rectangle(tuple(int(v*S) for v in box),fill=fill)
    for x in range(0,600,25): line([(x,0),(x,400)],'#1c2e40',1)
    for y in range(0,400,25): line([(0,y),(600,y)],'#1c2e40',1)
    ellipse((111,99,489,387),outline='#0c1721',width=48)
    ellipse((120,90,480,370),outline='#92601c',width=42)
    ellipse((124,94,476,366),outline='#e4b644',width=32)
    ellipse((130,100,470,360),outline='#ffe29a',width=7)
    ellipse((149,120,451,340),outline='#ba882f',width=4)
    rect((278,55,322,115),'#98631e'); rect((284,60,316,109),'#e7be5c')
    line([(288,64),(288,104)],'#fff2b1',3)
    if worn:
        for x,y in [(150,160),(432,160)]:
            d.polygon([(int(a*S),int(b*S)) for a,b in [(x-10,y-14),(x+5,y-11),(x+13,y+4),(x+5,y+13),(x-8,y+10),(x-14,y-3)]],fill='#a3b2bb')
            line([(x-9,y-7),(x+5,y+7)],'#475b6c',3)
            line([(x-4,y-10),(x+9,y+3)],'#dce6eb',2)
        rect((276,68,324,81),'#152332')
        line([(282,67),(291,72),(296,66)],'#6a411d',3)
        line([(303,83),(312,78),(318,83)],'#ffe29a',3)
    im.resize((600,400),Image.Resampling.LANCZOS).save(out/('bracelet_worn.png' if worn else 'bracelet_clean.png'))
bracelet(True); bracelet(False)
Image.new('RGBA',(16,16),(0,0,0,0)).save(out/'transparent.png')
im=Image.new('RGBA',(128*S,128*S)); d=ImageDraw.Draw(im)
d.ellipse((9*S,9*S,119*S,119*S),outline='#b6f5ce',width=5*S)
im.resize((128,128),Image.Resampling.LANCZOS).save(out/'marker.png')
Image.new('RGBA',(64,64),'#e7d9bb').save(out/'panel.png')
Image.new('RGBA',(64,32),'#315270').save(out/'button.png')
Image.new('RGBA',(64,32),'#427394').save(out/'button_hover.png')
print('Created original code-drawn bracelet schematic and UI sprites.')

