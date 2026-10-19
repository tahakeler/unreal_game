"""List poly.pizza search hits from a saved HTML page. Usage: python3 -I pp_search.py page.html"""
import re, sys
s = open(sys.argv[1], encoding="utf-8").read()
pat = re.compile(r'"title":"([^"]*)","alt":"[^"]*","creator":\{"username":"([^"]*)".*?"publicID":"([^"]+)","licence":"([^"]*)"')
for t, c, pid, lic in pat.findall(s):
    print(f"{pid}\t{lic}\t{c}\t{t}")
