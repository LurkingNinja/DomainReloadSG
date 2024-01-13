using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests
{
    public class DomainReloadSupportEditModeTest
    {
        private const string TEST_SCENE = "Assets/TestScene/TestScene.unity";
        
        [SetUp]
        public void SetupTest()
        {
            if (SceneManager.GetActiveScene().path == TEST_SCENE) return;
            
            var scene = EditorSceneManager.OpenScene(TEST_SCENE);
            SceneManager.SetActiveScene(scene);
        }
        
        [UnityTest]
        public IEnumerator TestStaticVariableReset()
        {
            yield return null;
            yield return new EnterPlayMode(false);
            yield return null;
            yield return new ExitPlayMode();
            yield return null;
            yield return new EnterPlayMode(false);
            Assert.AreEqual(1, TestScene.TestDomainReloadSupport.TestValue);
        }
    }
}
