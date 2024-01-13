using UnityEngine;

namespace TestScene
{
    public partial class TestDomainReloadSupport : MonoBehaviour
    {
        public static int TestValue = 0;
    
        void Start()
        {
            TestValue++;
            Debug.Log($"TestValue always should be 1 and it is {TestValue}");
        }
    }
}
