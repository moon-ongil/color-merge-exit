"""Authors levels for Color Exit and validates solvability with BFS.
Mirrors the C# Board semantics: exact-step moves, exit requires color+lane+orientation
match, each exit advances its own sequence, win = all target vehicles exited.
Emits JSON matching LevelData (enums as ints) to Assets/StreamingAssets/Levels/.
"""
import json, os
from collections import deque

# enum ints (must match C#)
R,B,Y,G,P,O = 0,1,2,3,4,5          # CarColor
H,V = 0,1                           # Orientation
TOP,BOT,LEFT,RIGHT = 0,1,2,3        # Edge

def cells(v):
    x,y,c,l,o,t = v
    return [(x+i,y) for i in range(l)] if o==H else [(x,y+i) for i in range(l)]

def solve(level, max_states=400000):
    W,H_,vehicles,exits = level['width'],level['height'],level['vehicles'],level['exits']
    obstacles=set((o['x'],o['y']) for o in level.get('obstacles',[]))
    # vehicle tuple: (x,y,color,length,orient,isTarget); keyed by id (index)
    vs=[(v['x'],v['y'],v['color'],v['length'],v['orientation'],v.get('isTarget',True)) for v in vehicles]
    ex=[(e['edge'],e['lane'],tuple(e['colorSequence'])) for e in exits]

    def occ(state_vs, present):
        s=set()
        for i in present:
            for c in cells(state_vs[i]): s.add(c)
        return s

    def exit_at(edge,lane):
        for k,(ed,ln,seq) in enumerate(ex):
            if ed==edge and ln==lane: return k
        return None

    start_present=frozenset(range(len(vs)))
    start_idx=tuple(0 for _ in ex)
    start=(tuple(vs), start_present, start_idx)

    def is_goal(state):
        svs,present,_=state
        return not any(svs[i][5] for i in present)  # no target present

    def neighbors(state):
        svs,present,eidx=state
        base_occ=occ(svs,present)
        for i in present:
            x,y,c,l,o,t=svs[i]
            my=set(cells(svs[i]))
            for dir in (1,-1):
                nx,ny=x,y
                # step outward
                cx,cy=x,y
                for step in range(1,max(W,H_)+2):
                    if o==H: lead=(nx+dir*(step)+ (l-1 if dir>0 else 0) if False else (x + (l if dir>0 else -1) + (dir*(step-1))), y)
                    # simpler: recompute leading cell for each step by tracking position
                    break
        # the above got messy; implement stepwise below
        return _neighbors(state,base_occ)

    def leading(vx,vy,o,l,dir):
        if o==H:
            return (vx+l, vy) if dir>0 else (vx-1, vy)
        else:
            return (vx, vy+l) if dir>0 else (vx, vy-1)

    def _neighbors(state, base_occ):
        svs,present,eidx=state
        out=[]
        for i in present:
            x,y,c,l,o,t=svs[i]
            others=base_occ - set(cells(svs[i]))
            for dir in (1,-1):
                vx,vy=x,y
                for step in range(1, max(W,H_)+2):
                    lc=leading(vx,vy,o,l,dir)
                    lx,ly=lc
                    inb = 0<=lx<W and 0<=ly<H_
                    if inb:
                        if lc in others or lc in obstacles:
                            break
                        # move one
                        if o==H: vx+=dir
                        else: vy+=dir
                        nvs=list(svs); nvs[i]=(vx,vy,c,l,o,t)
                        out.append((tuple(nvs),present,eidx))
                    else:
                        edge = (RIGHT if dir>0 else LEFT) if o==H else (BOT if dir>0 else TOP)
                        lane = vy if o==H else vx
                        k=exit_at(edge,lane)
                        if k is not None:
                            ed,ln,seq=ex[k]
                            if seq[eidx[k]%len(seq)]==c:
                                npres=set(present); npres.discard(i)
                                neidx=list(eidx); neidx[k]+=1
                                out.append((svs, frozenset(npres), tuple(neidx)))
                        break
        return out

    def key(state):
        svs,present,eidx=state
        return (tuple(sorted((i,svs[i][0],svs[i][1]) for i in present)), eidx)

    seen={key(start)}
    q=deque([(start,0)])
    while q:
        st,d=q.popleft()
        if is_goal(st): return d
        if len(seen)>max_states: return None
        for nb in _neighbors(st, occ(st[0],st[1])):
            kk=key(nb)
            if kk not in seen:
                seen.add(kk); q.append((nb,d+1))
    return None

# ---------------- level definitions ----------------
def veh(id,c,l,o,x,y,t=True): return {"id":id,"color":c,"length":l,"orientation":o,"x":x,"y":y,"isTarget":t}
def ext(edge,lane,seq): return {"edge":edge,"lane":lane,"colorSequence":seq}

LEVELS=[]

# 002: color sequence intro (red then blue through same exit)
LEVELS.append(dict(id=2,name="Sequence Start",width=6,height=6,
    timeLimitSeconds=80,star2SecondsLeft=20,star3SecondsLeft=35,
    vehicles=[veh(1,R,2,H,3,2), veh(2,B,2,H,0,2)],
    exits=[ext(RIGHT,2,[R,B])], obstacles=[]))

# 003: big car (len3) blocker
LEVELS.append(dict(id=3,name="Big Blocker",width=6,height=6,
    timeLimitSeconds=80,star2SecondsLeft=20,star3SecondsLeft=35,
    vehicles=[veh(1,R,2,H,0,2), veh(2,G,3,V,3,0,False)],
    exits=[ext(RIGHT,2,[R])], obstacles=[]))

# 004: two exits, two colors
LEVELS.append(dict(id=4,name="Two Exits",width=6,height=6,
    timeLimitSeconds=75,star2SecondsLeft=18,star3SecondsLeft=32,
    vehicles=[veh(1,R,2,H,1,1), veh(2,B,2,H,1,4),
              veh(3,Y,2,V,4,0,False)],
    exits=[ext(RIGHT,1,[R]), ext(RIGHT,4,[B])], obstacles=[]))

# 005: obstacle + blocker
LEVELS.append(dict(id=5,name="Detour",width=6,height=6,
    timeLimitSeconds=75,star2SecondsLeft=18,star3SecondsLeft=32,
    vehicles=[veh(1,R,2,H,0,3), veh(2,B,2,V,2,2,False), veh(3,Y,2,V,4,3,False)],
    exits=[ext(RIGHT,3,[R])], obstacles=[{"x":3,"y":1},{"x":1,"y":5}]))

# 006: three-color sequence, mixed sizes
LEVELS.append(dict(id=6,name="Three in a Row",width=6,height=6,
    timeLimitSeconds=70,star2SecondsLeft=16,star3SecondsLeft=30,
    vehicles=[veh(1,R,2,H,4,0), veh(2,Y,2,H,4,2), veh(3,G,2,H,4,4),
              veh(4,B,3,V,2,1,False)],
    exits=[ext(RIGHT,0,[R]), ext(RIGHT,2,[Y]), ext(RIGHT,4,[G])], obstacles=[]))

# 007: vertical exit (top) + horizontal blockers
LEVELS.append(dict(id=7,name="Way Up",width=6,height=6,
    timeLimitSeconds=70,star2SecondsLeft=16,star3SecondsLeft=30,
    vehicles=[veh(1,P,2,V,2,3), veh(2,O,2,H,1,2,False), veh(3,G,2,H,3,1,False)],
    exits=[ext(TOP,2,[P])], obstacles=[]))

if __name__=="__main__":
    outdir="/Users/moon/Developer/work/ongil/color-merge-exit/Assets/StreamingAssets/Levels"
    os.makedirs(outdir,exist_ok=True)
    allok=True
    for lv in LEVELS:
        d=solve(lv)
        status="SOLVABLE (%s moves)"%d if d is not None else "UNSOLVABLE"
        print(f"level_{lv['id']:03d} {lv['name']:<18} -> {status}")
        if d is None: allok=False; continue
        path=os.path.join(outdir,f"level_{lv['id']:03d}.json")
        with open(path,"w") as f: json.dump(lv,f,indent=2)
    print("ALL SOLVABLE" if allok else "SOME UNSOLVABLE - fix before shipping")
