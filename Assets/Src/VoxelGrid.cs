﻿using UnityEngine;
using System.Collections.Generic;

[SelectionBase]
public class VoxelGrid : MonoBehaviour {

	public int resolution;

	public GameObject voxelPrefab;

	public VoxelGrid xNeighbor, yNeighbor, xyNeighbor;

	private Voxel[] voxels;

	private float voxelSize, gridSize;

	//private Material[] voxelMaterials;

	private Mesh mesh;

	private List<Vector3> gen_vertices;
	private List<int>     gen_triangles;

	private List<Vector2> gen_uv;
	private Vector2[] vox_uv;

	private Voxel dummyX, dummyY, dummyT;
	private Noise noise;

	public void Initialize (int resolution, float size) {
		this.resolution = resolution;
		gridSize = size;
		voxelSize = size / (float)resolution;
		voxels = new Voxel[resolution * resolution];
		//voxelMaterials = new Material[voxels.Length];

		dummyX = new Voxel();
		dummyY = new Voxel();
		dummyT = new Voxel();

		noise = new Noise(Noise.DEFAULT_SEED);
		for (int i = 0, y = 0; y < resolution; y++) {
			for (int x = 0; x < resolution; x++, i++) {
				voxels[i] = CreateVoxel(x, y);
				voxels[i].SetVType(RandomVType(x * voxelSize, y * voxelSize));
			}
		}

		GetComponent<MeshFilter>().mesh = mesh = new Mesh();
		mesh.name = "VoxelGrid Mesh";

		// TODO: optimize this with preallocated array
		gen_vertices  = new List<Vector3>();
		gen_uv        = new List<Vector2>();
		vox_uv        = new Vector2[4];
		gen_triangles = new List<int>();
		Refresh();	
	}

	private Voxel CreateVoxel (int x, int y) {
		// Create white dots on voxel positions
		GameObject              white_dot = Instantiate(voxelPrefab) as GameObject;
		white_dot.transform.parent        = transform;
		white_dot.transform.localPosition = new Vector3(x * voxelSize, y * voxelSize, -0.01f);
		white_dot.transform.localScale    = Vector3.one * voxelSize * 0.1f;

		// Create map cell
		return new Voxel(x, y, voxelSize);
	}

	private VoxelType RandomVType(float x, float y) {
		//double vtype_norm = ((noise.eval(x, y) + 1f) / 2f);
		double vtype_norm = Mathf.PerlinNoise(x, y);
		int vtype = (int)(vtype_norm * (int)VoxelType.VoxelType_MaxValue);
		//Debug.Log ("x=" + x + " y=" + y + " vtype=" + vtype + " vtnorm=" + vtype_norm);
		return (VoxelType)vtype;
	}

	private void Refresh () {
		//SetVoxelColors();
		Triangulate();
	}
	
	private void Triangulate () {
		gen_vertices.Clear();
		gen_uv.Clear();
		gen_triangles.Clear();
		mesh.Clear();

		if (xNeighbor != null) {
			dummyX.BecomeXDummyOf(xNeighbor.voxels[0], gridSize);
		}
		TriangulateCellRows();
		if (yNeighbor != null) {
			TriangulateGapRow();
		}

		mesh.vertices  = gen_vertices.ToArray();
		mesh.triangles = gen_triangles.ToArray();
		mesh.uv        = gen_uv.ToArray();
		mesh.RecalculateNormals();
	}

	private void TriangulateCellRows () {
		int cells = resolution - 1;
		for (int i = 0, y = 0; y < cells; y++, i++) {
			for (int x = 0; x < cells; x++, i++) {
				TriangulateCell(
					voxels[i],
					voxels[i + 1],
					voxels[i + resolution],
					voxels[i + resolution + 1]);
			}
			if (xNeighbor != null) {
				TriangulateGapCell(i);
			}
		}
	}

	private void TriangulateGapCell (int i) {
		Voxel dummySwap = dummyT;
		dummySwap.BecomeXDummyOf(xNeighbor.voxels[i + 1], gridSize);
		dummyT = dummyX;
		dummyX = dummySwap;
		TriangulateCell(voxels[i], dummyT, voxels[i + resolution], dummyX);
	}

	private void TriangulateGapRow () {
		dummyY.BecomeYDummyOf(yNeighbor.voxels[0], gridSize);
		int cells = resolution - 1;
		int offset = cells * resolution;

		for (int x = 0; x < cells; x++) {
			Voxel dummySwap = dummyT;
			dummySwap.BecomeYDummyOf(yNeighbor.voxels[x + 1], gridSize);
			dummyT = dummyY;
			dummyY = dummySwap;
			TriangulateCell(voxels[x + offset], voxels[x + offset + 1], dummyT, dummyY);
		}

		if (xNeighbor != null) {
			dummyT.BecomeXYDummyOf(xyNeighbor.voxels[0], gridSize);
			TriangulateCell(voxels[voxels.Length - 1], dummyX, dummyY, dummyT);
		}
	}

	private void TriangulateCell (Voxel a, Voxel b, Voxel c, Voxel d) {
/*
		int cellType = 0;
		if (a.state) {
			cellType |= 1;
		}
		if (b.state) {
			cellType |= 2;
		}
		if (c.state) {
			cellType |= 4;
		}
		if (d.state) {
			cellType |= 8;
		}
		switch (cellType) {
		case 0:
			return;
		case 1:
			AddTriangle(a.position, a.yEdgePosition, a.xEdgePosition);
			break;
		case 2:
			AddTriangle(b.position, a.xEdgePosition, b.yEdgePosition);
			break;
		case 3:
			AddQuad(a.position, a.yEdgePosition, b.yEdgePosition, b.position);
			break;
		case 4:
			AddTriangle(c.position, c.xEdgePosition, a.yEdgePosition);
			break;
		case 5:
			AddQuad(a.position, c.position, c.xEdgePosition, a.xEdgePosition);
			break;
		case 6:
			AddTriangle(b.position, a.xEdgePosition, b.yEdgePosition);
			AddTriangle(c.position, c.xEdgePosition, a.yEdgePosition);
			break;
		case 7:
			AddPentagon(a.position, c.position, c.xEdgePosition, b.yEdgePosition, b.position);
			break;
		case 8:
			AddTriangle(d.position, b.yEdgePosition, c.xEdgePosition);
			break;
		case 9:
			AddTriangle(a.position, a.yEdgePosition, a.xEdgePosition);
			AddTriangle(d.position, b.yEdgePosition, c.xEdgePosition);
			break;
		case 10:
			AddQuad(a.xEdgePosition, c.xEdgePosition, d.position, b.position);
			break;
		case 11:
			AddPentagon(b.position, a.position, a.yEdgePosition, c.xEdgePosition, d.position);
			break;
		case 12:
			AddQuad(a.yEdgePosition, c.position, d.position, b.yEdgePosition);
			break;
		case 13:
			AddPentagon(c.position, d.position, b.yEdgePosition, a.xEdgePosition, a.position);
			break;
		case 14:
			AddPentagon(d.position, b.position, a.xEdgePosition, a.yEdgePosition, c.position);
			break;
		case 15:
			AddQuad(a.position, c.position, d.position, b.position);
			break;
		}
*/
		if (a.vtype != VoxelType.Empty) {
			AddQuad(a.position, b.position, c.position, d.position);

			a.GetUV(vox_uv);
			gen_uv.Add(vox_uv[0]);
			gen_uv.Add(vox_uv[1]);
			gen_uv.Add(vox_uv[2]);
			gen_uv.Add(vox_uv[3]);
		}
	}

	/*
	private void AddTriangle (Vector3 a, Vector3 b, Vector3 c) {
		int vertexIndex = gen_vertices.Count;
		gen_vertices.Add(a);
		gen_vertices.Add(b);
		gen_vertices.Add(c);
		gen_triangles.Add(vertexIndex);
		gen_triangles.Add(vertexIndex + 1);
		gen_triangles.Add(vertexIndex + 2);
	}
	*/

	private void AddQuad (Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
		int vertexIndex = gen_vertices.Count;
		gen_vertices.Add(a);
		gen_vertices.Add(b);
		gen_vertices.Add(c);
		gen_vertices.Add(d);

		gen_triangles.Add(vertexIndex);
		gen_triangles.Add(vertexIndex + 2);
		gen_triangles.Add(vertexIndex + 3);
		gen_triangles.Add(vertexIndex);
		gen_triangles.Add(vertexIndex + 3);
		gen_triangles.Add(vertexIndex + 1);
	}

	/*
	private void AddPentagon (Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e) {
		int vertexIndex = gen_vertices.Count;
		gen_vertices.Add(a);
		gen_vertices.Add(b);
		gen_vertices.Add(c);
		gen_vertices.Add(d);
		gen_vertices.Add(e);
		gen_triangles.Add(vertexIndex);
		gen_triangles.Add(vertexIndex + 1);
		gen_triangles.Add(vertexIndex + 2);
		gen_triangles.Add(vertexIndex);
		gen_triangles.Add(vertexIndex + 2);
		gen_triangles.Add(vertexIndex + 3);
		gen_triangles.Add(vertexIndex);
		gen_triangles.Add(vertexIndex + 3);
		gen_triangles.Add(vertexIndex + 4);
	}
	*/
	
	/*private void SetVoxelColors () {
		for (int i = 0; i < voxels.Length; i++) {
			voxelMaterials[i].color = voxels[i].GetColor();
		}
	}*/

	public void Apply (VoxelStencil stencil) {
		int xStart = stencil.XStart;
		if (xStart < 0) {
			xStart = 0;
		}
		int xEnd = stencil.XEnd;
		if (xEnd >= resolution) {
			xEnd = resolution - 1;
		}
		int yStart = stencil.YStart;
		if (yStart < 0) {
			yStart = 0;
		}
		int yEnd = stencil.YEnd;
		if (yEnd >= resolution) {
			yEnd = resolution - 1;
		}

		//VoxelType current_vtype = VoxelType.Dirt;
		for (int y = yStart; y <= yEnd; y++) {
			int i = y * resolution + xStart;
			for (int x = xStart; x <= xEnd; x++, i++) {
				voxels[i].SetVType(stencil.Apply(x, y, voxels[i].vtype));
			}
		}
		Refresh();
	}
}