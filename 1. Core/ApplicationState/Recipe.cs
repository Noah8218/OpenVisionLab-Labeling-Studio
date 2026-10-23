using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace MvcVisionSystem
{
    public class Recipe
    {
        // xml 저장과 상관 없는 데이터 분리. => XmlIgnore
        // 설계에서 설정값과 runtime에 관리하는 data를 분리할 것.
        [XmlIgnore] public EventHandler<EventArgs> EventChagedRecipe;

        // xml serialize는 property 만 가능합니다.
        // 변수 선언하고 alt + enter로 property를 자동 생성 하세요. => 필드 캡슐화
        // 간단한건 attribute로. 너무 많을 땐 element로
        // 필요없는건 XmlIgnore

        //int, double, bool 등 일반 자료형일 경우 애트리뷰트로 선언하는게 바람직
        // 예 <IData MAX_CELL_COUNT="10"/>
        //[XmlAttribute]

        /*
         * <root> // 앨리먼트
                 <Count> 1 </Count> // 애트리뷰트
            </root>
         *
         * */

        // 복수일떄 s를 붙히는게 바람직
        //[XmlArray("HeadUseInfos")]
        //[XmlArrayItem("HeadUseInfo")]

        [XmlIgnore] private const string RECIPE_DIRECTORY = "RECIPE";

        [XmlIgnore] private string m_strName = "";

        [XmlIgnore] private string m_strNamePrev = "";

        [XmlIgnore]
        public string Name
        {
            get { return m_strName; }
            set
            {
                // Recipe identity must not replace LabelingApplicationState.Data. The WPF session
                // service loads and commits validated data before calling
                // CommitLoadedRecipe.
                CommitLoadedRecipe(value);
            }
        }

        public Recipe() { }

        public string ModelNo { get; set; } = "";
        public string ModelName { get; set; } = "";

        internal void CommitLoadedRecipe(string recipeName)
        {
            SetRecipeIdentity(recipeName);
            if (m_strName != "")
            {
                CompleteRecipeTransition();
            }
            else if (m_strNamePrev != "")
            {
                EventChagedRecipe?.Invoke(null, null);
            }
        }

        private void SetRecipeIdentity(string recipeName)
        {
            string normalizedName = recipeName ?? string.Empty;
            m_strNamePrev = m_strName;
            m_strName = normalizedName;

            if (m_strName.Length == 0)
            {
                ModelName = string.Empty;
                ModelNo = string.Empty;
                return;
            }

            try
            {
                ModelName = Name.Substring(0, Name.Length - 3);
                ModelNo = Name.Substring(Name.Length - 3);
            }
            catch { }
        }

        private void CompleteRecipeTransition()
        {
            InitDirectory(m_strName);
            EventChagedRecipe?.Invoke(null, null);
        }

        public static bool InitDirectory(string strRecipeName)
        {
            try
            {
                string strRecipePath = Path.Combine(AppContext.BaseDirectory, RECIPE_DIRECTORY);
                DirectoryInfo dirRecipe = new DirectoryInfo(strRecipePath);
                if (dirRecipe.Exists == false) dirRecipe.Create();

                string strRecipeNamePath = Path.Combine(AppContext.BaseDirectory, RECIPE_DIRECTORY, strRecipeName ?? string.Empty);
                DirectoryInfo dirRecipeName = new DirectoryInfo(strRecipeNamePath);
                if (dirRecipeName.Exists == false) dirRecipeName.Create();

                return true;
            }
            catch (Exception Desc)
            {
                AppLog.ABNORMAL( $"[FAILED] {MethodBase.GetCurrentMethod().ReflectedType.Name}==>{MethodBase.GetCurrentMethod().Name}   Exception ==> {Desc.Message}");
                return false;
            }
        }
    }
}
