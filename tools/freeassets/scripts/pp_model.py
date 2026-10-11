"""Print id, licence, animated, tri count, title, creator, glb url from a saved poly.pizza model page."""
import re, sys
for p in sys.argv[1:]:
    s = open(p, encoding="utf-8").read()
    g = lambda r: (re.search(r, s) or [None, "?"])[1]
    print("\t".join([p.split("/")[-1], g(r'"Licence":"([^"]+)"'), g(r'"Animated":(\w+)'), g(r'"Tri[cC]ount":(\d+)'),
                     g(r'"Title":"([^"]+)"'), g(r'"Creator":\{[^}]*?"Username":"([^"]+)"'),
                     g(r'(https://static\.poly\.pizza/[0-9a-f-]+\.glb)')]))
