# Description

![IMG](https://img.shields.io/badge/Platform-Window10-lightgrey) ![IMG](https://img.shields.io/badge/Visual%20Studio-2019-8d59cb) ![IMG](https://img.shields.io/badge/Unity-2020.1-222222) ![cpu](https://img.shields.io/badge/CPU-Intel%20i7--7700HQ-blue) ![gpu](https://img.shields.io/badge/GPU-NVidia%20GTX%201050-green)



This is an implementation of SigGraph 16' Paper, [Shrödinger's Smoke](http://page.math.tu-berlin.de/~chern/projects/SchrodingersSmoke/).

I do it for final project in Advances in Computer graphics lectured by Prof. Kun Zhou.



Below sections is sampled from my final report.

### Highlights

In the implementation, I implemented the incompressible Schrödinger fluid **(ISF)** algorithm described by the author, and completed the nozzle scene and LeapFrog scene.

In terms of performance, the author uses Houdini, 128^3^ subdivided mesh to solve ISF takes less than 1s, but I use Unity with Compute Shader to perform calculations on GPU, 512x128x128 subdivided mesh can solve ISF in about 200ms ,faster. When simulating particles, I used 400 million particles and finally simulated smoke at 1 fps. If you use Billboard technology with textures, you can use only tens of thousands of particles, and you can achieve real-time simulation (150 fps) on the GTX 1050 while reducing a certain sense of reality.

The highlights of my implementation is that the particle system, FFT calculation, and ISF solution are **all calculated on the GPU**, and the CPU is only responsible for sending parameters and random seeds. This is much faster than Author’s implementation even with an low end GPU & CPU.



### Results

#### 5.2.1 LeapFrog

| My Implementation                                            | Author’s Implementation                                      |
| ------------------------------------------------------------ | ------------------------------------------------------------ |
| ![image-20200627201609546](img/readme/image-20200627201609546.png) | ![image-20200627201620949](img/readme/image-20200627201620949.png) |
| ![image-20200627201642435](img/readme/image-20200627201642435.png) | ![image-20200627201700770](img/readme/image-20200627201700770.png) |
| ![image-20200627201720950](img/readme/image-20200627201720950.png) | ![image-20200627201738559](img/readme/image-20200627201738559.png) |
|                                                              |                                                              |



#### 5.2.2 Nozzle

My Implementation, before fine tuning:

![image-20200628190801999](img/readme/image-20200628190801999.png)

My Implementation, after fine tuning:

![image-20200628190936930](img/readme/image-20200628190936930.png)



My implemetation:

![image-20200627201904720](img/readme/image-20200627201904720.png)

Author‘s  implementation:

![image-20200628191040036](img/readme/image-20200628191040036.png)



Different Parameters

 $\hbar=0.1$

![image-20200628191127175](img/readme/image-20200628191127175.png)

 $\hbar=0.05$

![image-20200628191538628](img/readme/image-20200628191538628.png)

 $\hbar = 0.2$

![image-20200628191903067](img/readme/image-20200628191903067.png)


