"""Layout: camp bottom-right, wildlife spread, extra trees/rocks/animals."""
from __future__ import annotations

import re
from pathlib import Path

SCENE = Path(r"C:\Users\antho\senior\Assets\Scenes\GetStarted_Scene.unity")
DX, DZ = 16.0, -14.0

# Exact transform lines to replace (camp cluster + prefab mod values)
POSITION_REPLACEMENTS = {
    "m_LocalPosition: {x: 0, y: 2, z: 0}": "m_LocalPosition: {x: 16, y: 2, z: -14}",
    "m_LocalPosition: {x: -16.69, y: 0, z: -9.39}": "m_LocalPosition: {x: -0.69, y: 0, z: -23.39}",
    "m_LocalPosition: {x: 10.08, y: 0, z: -2.16}": "m_LocalPosition: {x: 26.08, y: 0, z: -16.16}",
    "m_LocalPosition: {x: 5.06, y: 0, z: -6.01}": "m_LocalPosition: {x: 21.06, y: 0, z: -20.01}",
    "m_LocalPosition: {x: -12.88, y: 0, z: 4}": "m_LocalPosition: {x: 3.12, y: 0, z: -10}",
    "m_LocalPosition: {x: 4, y: 0, z: 4}": "m_LocalPosition: {x: 20, y: 0, z: -10}",
    "m_LocalPosition: {x: -9.32, y: 0, z: -18.15}": "m_LocalPosition: {x: 8, y: 0, z: -22}",
    "m_LocalPosition: {x: 0, y: 1, z: 0}": "m_LocalPosition: {x: 0, y: 1, z: 22}",
    "m_LocalPosition: {x: 0, y: 1, z: 8}": "m_LocalPosition: {x: -14, y: 1, z: 14}",
    "m_LocalPosition: {x: 6, y: 1, z: 5}": "m_LocalPosition: {x: 10, y: 1, z: 16}",
    "m_LocalPosition: {x: -5, y: 1, z: 6}": "m_LocalPosition: {x: -10, y: 1, z: 20}",
    "m_LocalPosition: {x: -10, y: 1, z: 15}\n  m_LocalScale: {x: 2, y: 2, z: 2}": "m_LocalPosition: {x: 18, y: 1, z: 8}\n  m_LocalScale: {x: 2, y: 2, z: 2}",
    "m_LocalPosition: {x: 15, y: 1, z: 20}": "m_LocalPosition: {x: -20, y: 1, z: 6}",
}

PREFAB_XZ = [
    (-3.3950346, -4.002865),
    (-6.35, -5.3),
    (-5.814424, -1.5285592),
    (-2.5, -11.54),
    (-5.4302297, -2.2087166),
    (-5.92, -5.08),
]


def shift_prefab_positions(text: str) -> str:
    for x, z in PREFAB_XZ:
        nx, nz = x + DX, z + DZ
        text = text.replace(
            f"propertyPath: m_LocalPosition.x\n      value: {x}",
            f"propertyPath: m_LocalPosition.x\n      value: {nx}",
        )
        text = text.replace(
            f"propertyPath: m_LocalPosition.z\n      value: {z}",
            f"propertyPath: m_LocalPosition.z\n      value: {nz}",
        )
    return text


def clone_tree(name: str, x: float, y: float, z: float, ids: tuple[int, ...]) -> str:
    go, mb, col, rend, mesh, tr = ids
    return f"""--- !u!1 &{go}
GameObject:
  m_Component:
  - component: {{fileID: {tr}}}
  - component: {{fileID: {mesh}}}
  - component: {{fileID: {rend}}}
  - component: {{fileID: {col}}}
  - component: {{fileID: {mb}}}
  m_Name: {name}
  m_IsActive: 1
--- !u!114 &{mb}
MonoBehaviour:
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_Script: {{fileID: 11500000, guid: 327d60a3648dd4c429c2c4ef503cc9e1, type: 3}}
  woodAmount: 3
  chopRange: 3
--- !u!136 &{col}
CapsuleCollider:
  m_GameObject: {{fileID: {go}}}
  m_Radius: 0.5
  m_Height: 2
  m_Direction: 1
--- !u!23 &{rend}
MeshRenderer:
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_Materials:
  - {{fileID: 2100000, guid: 9845b38987b69224ca6524623f877af8, type: 2}}
--- !u!33 &{mesh}
MeshFilter:
  m_GameObject: {{fileID: {go}}}
  m_Mesh: {{fileID: -6831203800605888285, guid: 2bb56834cfe7c034c9f64fd80f14f6a5, type: 3}}
--- !u!4 &{tr}
Transform:
  m_GameObject: {{fileID: {go}}}
  m_LocalPosition: {{x: {x}, y: {y}, z: {z}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Father: {{fileID: 0}}
"""


def clone_animal(name: str, x: float, y: float, z: float, kind: int, scale: float, ids: tuple[int, ...]) -> str:
    go, col, rend, mesh, tr, ani = ids
    return f"""--- !u!1 &{go}
GameObject:
  m_Component:
  - component: {{fileID: {tr}}}
  - component: {{fileID: {mesh}}}
  - component: {{fileID: {rend}}}
  - component: {{fileID: {col}}}
  - component: {{fileID: {ani}}}
  m_Name: {name}
  m_IsActive: 1
--- !u!114 &{ani}
MonoBehaviour:
  m_GameObject: {{fileID: {go}}}
  m_Script: {{fileID: 11500000, guid: 6636e2ac8467d1e4da3c32dcf13c4073, type: 3}}
  kind: {kind}
  meatAmount: {1 if kind == 0 else 2}
--- !u!136 &{col}
CapsuleCollider:
  m_GameObject: {{fileID: {go}}}
  m_Radius: 0.5
  m_Height: 2
--- !u!23 &{rend}
MeshRenderer:
  m_GameObject: {{fileID: {go}}}
  m_Materials:
  - {{fileID: 2100000, guid: 31321ba15b8f8eb4c954353edc038b1d, type: 2}}
--- !u!33 &{mesh}
MeshFilter:
  m_GameObject: {{fileID: {go}}}
  m_Mesh: {{fileID: 10208, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!4 &{tr}
Transform:
  m_GameObject: {{fileID: {go}}}
  m_LocalPosition: {{x: {x}, y: {y}, z: {z}}}
  m_LocalScale: {{x: {scale}, y: {scale}, z: {scale}}}
  m_Father: {{fileID: 0}}
"""


def rock_prefab(pid: int, x: float, z: float) -> str:
    g = "ddbd7b661b923f949bf848f828a8cc50"
    return f"""--- !u!1001 &{pid}
PrefabInstance:
  m_Modification:
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
    - target: {{fileID: -8679921383154817045, guid: {g}, type: 3}}
      propertyPath: m_LocalPosition.x
      value: {x}
    - target: {{fileID: -8679921383154817045, guid: {g}, type: 3}}
      propertyPath: m_LocalPosition.y
      value: 0
    - target: {{fileID: -8679921383154817045, guid: {g}, type: 3}}
      propertyPath: m_LocalPosition.z
      value: {z}
  m_SourcePrefab: {{fileID: 100100000, guid: {g}, type: 3}}
"""


def main():
    text = SCENE.read_text(encoding="utf-8")
    for old, new in POSITION_REPLACEMENTS.items():
        if old in text:
            text = text.replace(old, new, 1)
            print("replaced", old[:40])
    text = shift_prefab_positions(text)

    roots = []
    blob = []
    blob.append(clone_tree("Tree_6", -12, 0, 18, (2100001001, 2100001002, 2100001003, 2100001004, 2100001005, 2100001006)))
    roots.append("2100001006")
    blob.append(clone_tree("Tree_7", 8, 0, 20, (2100001011, 2100001012, 2100001013, 2100001014, 2100001015, 2100001016)))
    roots.append("2100001016")
    blob.append(clone_tree("Tree_8", -18, 0, 12, (2100001021, 2100001022, 2100001023, 2100001024, 2100001025, 2100001026)))
    roots.append("2100001026")
    blob.append(clone_animal("Deer_2", -5, 1, 8, 1, 1.0, (2100001031, 2100001032, 2100001033, 2100001034, 2100001035, 2100001036)))
    roots.append("2100001035")
    blob.append(clone_animal("Bird_2", 6, 1, 12, 0, 0.5, (2100001041, 2100001042, 2100001043, 2100001044, 2100001045, 2100001046)))
    roots.append("2100001045")
    for i, (x, z) in enumerate([(-16, 10), (12, 14), (0, 10), (20, 18), (-8, 5)]):
        pid = 2100001050 + i
        blob.append(rock_prefab(pid, x, z))
        roots.append(str(pid))

    extra = "\n".join(blob)
    root_lines = "\n".join(f"  - {{fileID: {r}}}" for r in roots)
    text = text.replace(
        "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_Roots:\n",
        "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_Roots:\n" + root_lines + "\n",
    )
    text = text.replace(
        "--- !u!1660057539 &9223372036854775807\nSceneRoots:",
        extra + "\n--- !u!1660057539 &9223372036854775807\nSceneRoots:",
    )

    SCENE.write_text(text, encoding="utf-8")
    print("done")


if __name__ == "__main__":
    main()
