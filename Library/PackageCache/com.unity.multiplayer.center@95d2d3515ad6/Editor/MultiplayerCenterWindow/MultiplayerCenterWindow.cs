using System.Collections.Generic;
using Unity.Multiplayer.Center.Common;
using Unity.Multiplayer.Center.Editor.Analytics;
using Unity.Properties;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Multiplayer.Center.Editor
{
    class MultiplayerCenterWindow : EditorWindow, IDataSourceViewHashProvider
    {
        /// <summary>
        /// The window template is set up in the MonoScript
        /// so we don't have to rely on the file path in the code.
        /// </summary>
        [SerializeField] VisualTreeAsset m_WindowTemplate;

        /// <summary>
        /// The game genres container for the questionnaire.
        /// It is set up in the MonoScript so we don't have to rely on the file path in the code.
        /// </summary>
        [SerializeField] GameGenreList m_GameGenres;

        /// <summary>
        /// The ordered list of categories that are displayed in the onboarding window.
        /// </summary>
        [SerializeField] CategoriesDescription m_CategoriesDescription;

        [SerializeField] StyleSheet lightSkin;
        [SerializeField] StyleSheet darkSkin;

        /// <summary>
        /// Menu item declaration to open the window.
        /// </summary>
        [MenuItem("Window/Multiplayer/Multiplayer Center")]
        internal static void OpenWindow()
        {
            var window = GetWindow<MultiplayerCenterWindow>(false, "Multiplayer Center", true);
            window.minSize = new Vector2(600, 400);
            WindowChangedEvent.Send(new WindowChangedData(WindowTransition.Opened));
        }

        /// <summary>
        /// This is only for the window to survive an assembly reload
        /// and not reset the current m_DisplayQuestion state in its OnEnable.
        /// When users have already answered the question and would go back to the question page,
        /// if an assembly reload happens at this point, OnEnable will be called
        /// and we would reset the m_DisplayQuestion value, effectively leaving that panel.
        /// </summary>
        /// <remarks>
        /// This value is not saved outside the window instance context so that closing and
        /// reopening the window after having answered the question will bring users back
        /// to the quickstart content.
        /// </remarks>
        bool m_SetupDone;

        /// <summary>
        /// Which genre is currently selected based on the index of the <see cref="GameGenreList"/>.
        /// </summary>
        /// <remarks>
        /// This information is directly saved into the ProjectSettings.
        /// </remarks>
        int SelectedGenre
        {
            get => MultiplayerCenterSettings.instance.SelectedGenre;
            set
            {
                MultiplayerCenterSettings.instance.SelectedGenre = value;
                MultiplayerCenterSettings.instance.Save();
            }
        }

        /// <summary>
        /// Global state of the UI.
        /// </summary>
        /// <remarks>
        /// This is bound to the <see cref="QuestionModeSwitcher.QuestionMode"/> property in the UXML.
        /// </remarks>
        [CreateProperty] bool m_DisplayQuestion = true;

        /// <summary>
        /// Title label of the banner that changes based on the question and its answer.
        /// </summary>
        /// <remarks>
        /// This is bound to the "title" Label.text property in the UXML.
        /// </remarks>
        [CreateProperty]
        string TitleLabel => m_DisplayQuestion
            ? L10n.Tr("What kind of multiplayer game do you want to build?")
            : m_GameGenres.Descriptions[SelectedGenre].Name;

        /// <summary>
        /// Selected GameGenre in the ListView when <see cref="m_DisplayQuestion" /> is true.
        /// </summary>
        /// <remarks>
        /// This is bound to the "question-list" ListView.selectedIndex property in the UXML.
        /// </remarks>
        [CreateProperty] int m_ListViewSelectedGameGenre;

        /// <summary>
        /// Description of the selected GameGenre.
        /// </summary>
        /// <remarks>
        /// This is bound to the "question-description" VisualElement.dataSource property in the UXML.
        /// </remarks>
        [CreateProperty]
        GameGenreDescription SelectedGameGenre => m_GameGenres.Descriptions[m_ListViewSelectedGameGenre];

        /// <summary>
        /// Currently selected category.
        /// </summary>
        /// <remarks>
        /// This information is directly saved into the EditorPrefs because it is local to each user.
        /// It could be moved to be saved per-user-per-project in the future. <br />
        /// This is bound to the "categories" ListView.selectedIndex property in the UXML.
        /// </remarks>
        [CreateProperty]
        int SelectedCategory
        {
            get => m_SelectedCategory;
            set
            {
                if (value != m_SelectedCategory)
                {
                    m_SelectedCategory = value;
                    EditorPrefs.SetInt("com.unity.multiplayer.center.selectedCategory", value);
                }
            }
        }

        int m_SelectedCategory;

        /// <summary>
        /// Currently selected category type.
        /// </summary>
        /// <remarks>
        /// This is bound to the "category-content" <see cref="CategoriesContainer.DisplayedCategory"/> property in the UXML.
        /// </remarks>
        [CreateProperty]
        OnboardingSectionCategory SelectedCategoryType
        {
            get
            {
                var categories = m_CategoriesDescription.FilteredCategories;
                if (SelectedCategory < 0 || SelectedCategory >= categories.Count)
                    return default;
                return categories[SelectedCategory].CategoryType;
            }
        }

        void OnDestroy()
        {
            WindowChangedEvent.Send(new WindowChangedData(WindowTransition.Closed));
        }

        void OnEnable()
        {

            if (!m_SetupDone)
            {
                m_DisplayQuestion = SelectedGenre == -1;
                m_ListViewSelectedGameGenre = SelectedGenre < 0 ? 0 : SelectedGenre;
                m_SelectedCategory = EditorPrefs.GetInt("com.unity.multiplayer.center.selectedCategory", 0);
                m_SetupDone = true;
            }
        }

        /// <summary>
        /// Called by Unity automatically when the window is opened or the UI needs a full rebuild.
        /// </summary>
        void CreateGUI()
        {
            m_WindowTemplate.CloneTree(rootVisualElement);
            rootVisualElement.viewDataKey = GetType().Name;
            rootVisualElement.styleSheets.Add(EditorGUIUtility.isProSkin ? darkSkin : lightSkin);
            rootVisualElement.dataSource = this;

            var questionList = rootVisualElement.Q<ListView>("question-list");
            questionList.selectionChanged += OnQuestionListSelectionChanged;

            var answerButton = rootVisualElement.Q<Button>("answer-button");
            var backButton = rootVisualElement.Q<Button>("back-button");

            backButton.clicked += () =>
            {
                m_DisplayQuestion = true;
                SelectedGenre = -1;
            };

            answerButton.clicked += () =>
            {
                // Only save the genre and reset the category if it has changed.
                if (SelectedGenre != m_ListViewSelectedGameGenre)
                {
                    SelectedCategory = 0;
                    SelectedGenre = m_ListViewSelectedGameGenre;
                }

                m_DisplayQuestion = false;

                var genreName = SelectedGameGenre.Name;
                GenreSelectedEvent.Send(new GenreSelectedData { gameGenre = genreName });
            };
        }

        void OnQuestionListSelectionChanged(IEnumerable<object> objects)
        {
            var questionDescription = rootVisualElement.Q<VisualElement>("question-description");

            using var enumerator = objects.GetEnumerator();
            if (!enumerator.MoveNext()) return;

            var genre = enumerator.Current as GameGenreDescription?;
            var header = questionDescription.Q<VisualElement>("genre-header-container");
            header.Clear();

            var genreHeader = genre?.Header.Instantiate();
            genreHeader?.SetBinding(nameof(genreHeader.dataSource), new DataBinding() { dataSource = genre, });

            header.Add(genreHeader);
        }


        /// <summary>
        /// This is testing on the 3 fields with [CreateProperty] that have an impact on the UI display.
        /// </summary>
        /// <returns>A hash of the <see cref="m_DisplayQuestion"/>,
        /// <see cref="m_ListViewSelectedGameGenre"/>,
        /// and <see cref="SelectedCategory"/> values.</returns>
        public long GetViewHashCode()
        {
            var hash = Hash128.Compute(m_DisplayQuestion.GetHashCode());
            hash.Append(m_ListViewSelectedGameGenre);
            hash.Append(SelectedCategory);
            return hash.GetHashCode();
        }
    }
}
