from mpl_toolkits.mplot3d import Axes3D

import matplotlib.pyplot as plt
import json
import numpy as np

fig = plt.figure()
ax = fig.add_subplot(111, projection='3d')

with open('test/isf.nozzle.json') as f:
    u = json.load(f)['data']['f1']
    x, y, z = u
    





