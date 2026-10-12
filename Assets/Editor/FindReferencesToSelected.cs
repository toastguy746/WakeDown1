#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Hierarchy에서 오브젝트를 선택한 뒤 상단 메뉴 Tools > Find References To Selected (Scene)을 누르면,
// 현재 열려있는 씬의 모든 컴포넌트를 훑어서 "선택한 오브젝트(또는 그 오브젝트에 붙은 컴포넌트)를
// 참조하고 있는 필드"를 콘솔에 찍어줍니다. 로그를 클릭하면 참조하는 쪽 오브젝트가 하이라이트됩니다.
//
// [설치] 이 파일을 Assets/Editor/ 폴더 안에 넣으세요 (Editor 폴더가 없으면 만들면 됩니다).
//
// [찾을 수 있는 것] 컴포넌트의 public/[SerializeField] 필드, 리스트/배열 안의 참조,
//                  Button.onClick 같은 UnityEvent에 연결된 대상
// [찾을 수 없는 것] 문자열 이름으로 찾는 코드(GameObject.Find("이름")), Animation 클립 안의 경로,
//                  열려있지 않은 다른 씬이나 프리팹 에셋 안의 참조
public static class FindReferencesToSelected
{
    [MenuItem("Tools/Find References To Selected (Scene)")]
    private static void Find()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0)
        {
            Debug.LogWarning("[참조 검색] Hierarchy에서 오브젝트를 먼저 선택해주세요.");
            return;
        }

        // 찾을 대상 = 선택한 GameObject 자신 + 그 오브젝트에 붙은 모든 컴포넌트
        // (스크립트 필드가 Transform 같은 컴포넌트 타입이면 GameObject가 아니라 그 컴포넌트를 참조하기 때문)
        HashSet<Object> targets = new HashSet<Object>();
        foreach (GameObject go in selected)
        {
            targets.Add(go);
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c != null) targets.Add(c);
            }
        }

        int foundCount = 0;

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component comp in root.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue; // Missing Script
                    if (comp is Transform) continue; // Transform의 부모/자식 링크는 "사용"이 아니라 계층 구조일 뿐이라 제외

                    SerializedObject so = new SerializedObject(comp);
                    SerializedProperty it = so.GetIterator();

                    while (it.NextVisible(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (it.propertyPath == "m_GameObject") continue; // 모든 컴포넌트가 자기 GameObject를 가리키는 기본 필드

                        Object referenced = it.objectReferenceValue;
                        if (referenced == null || !targets.Contains(referenced)) continue;

                        Debug.Log(
                            $"[참조 발견] '{GetPath(comp.gameObject)}' 의 {comp.GetType().Name}.{it.propertyPath} → '{referenced.name}' ({referenced.GetType().Name})",
                            comp.gameObject);
                        foundCount++;
                    }
                }
            }
        }

        if (foundCount == 0)
        {
            Debug.Log($"[참조 없음] 선택한 오브젝트 {selected.Length}개를 참조하는 컴포넌트가 열려있는 씬에서 발견되지 않았습니다.");
        }
        else
        {
            Debug.Log($"[참조 검색 완료] 총 {foundCount}곳에서 참조 발견.");
        }
    }

    // 오브젝트의 하이라키 경로(예: Canvas/Panel/Button)를 만들어서 어디 있는 오브젝트인지 알아보기 쉽게 함
    private static string GetPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}
#endif