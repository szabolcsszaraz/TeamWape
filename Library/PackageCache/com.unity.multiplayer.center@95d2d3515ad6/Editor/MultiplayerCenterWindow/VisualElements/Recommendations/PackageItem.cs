using Unity.Properties;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine.UIElements;

namespace Unity.Multiplayer.Center.Editor
{
    /// <summary>
    /// This element is used as a parent of all VisualElements using <see cref="DelayedPackageBinding{T}"/>.
    /// It is using a <see cref="PackageInfoBinding"/> to update
    /// its children <see cref="VisualElement.dataSource"/> with a <see cref="PackageInfo"/>
    /// once obtained from the PackageManager API.
    /// </summary>
    [UxmlElement]
    sealed partial class PackageItem : VisualElement
    {
        string m_PackageId;
        SearchRequest m_Search;

        [UxmlAttribute,CreateProperty]
        public string PackageId
        {
            get => m_PackageId;
            set
            {
                if (m_PackageId != value)
                {
                    m_PackageId = value;
                    m_CustomBinding.PackageId = value;
                }
            }
        }

        PackageInfoBinding m_CustomBinding;

        public PackageItem()
        {
            m_CustomBinding = new PackageInfoBinding() { PackageId = PackageId };
            contentContainer.SetBinding(nameof(dataSource), m_CustomBinding);
        }
    }

    [UxmlObject]
    partial class PackageInfoBinding : CustomBinding
    {
        string m_PackageId;
        public string PackageId
        {
            get
            {
                return m_PackageId;
            }
            set
            {
                if (value == typeof(PackageInfo).FullName)
                {
                    return;
                }
                if (m_PackageId != value)
                {
                    m_PackageId = value;
                    MarkDirty();
                }
            }
        }
        SearchRequest m_Search;

        public PackageInfoBinding()
        {
            updateTrigger = BindingUpdateTrigger.WhenDirty;
        }

        protected override BindingResult Update(in BindingContext context)
        {
            if (m_Search == null || m_Search.PackageIdOrName != m_PackageId)
            {
                if(!string.IsNullOrWhiteSpace(m_PackageId))
                    m_Search = Client.Search(m_PackageId);
                else
                    m_Search = null;
            }

            if (m_Search!= null && m_Search.IsCompleted)
            {
                if (m_Search.Status == StatusCode.Success && m_Search.Result != null && m_Search.Result.Length > 0)
                {
                    var element = context.targetElement;
                    if (ConverterGroups.TrySetValueGlobal(ref element, context.bindingId, m_Search.Result[0], out var errorCode))
                        return new BindingResult(BindingStatus.Success);

                    return new BindingResult(BindingStatus.Failure, $"Failed to set value with error code: {errorCode}");
                }

                return new BindingResult(BindingStatus.Failure, $"Failed to find package name '{m_PackageId}' to resolve binding.");
            }
            return new BindingResult(BindingStatus.Pending);
        }
    }
}
