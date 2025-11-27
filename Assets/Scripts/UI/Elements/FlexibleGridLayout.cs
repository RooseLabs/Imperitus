using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI.Elements
{
    public class FlexibleGridLayout : GridLayoutGroup
    {
        public enum ExpandMode
        {
            ExpandRows,
            ExpandColumns
        }

        public enum FlowMode
        {
            Horizontally,
            Vertically
        }

        #region Serialized
        [Min(1)] public int minColumnCount = 3;
        [Min(1)] public int minRowCount = 3;

        [Tooltip("Automatically resize grid (number of rows and columns) based on number of children")]
        public bool autoResizeGrid;

        [Tooltip("Apply a custom aspect ratio to cells")]
        public bool maintainCellRatio;

        [Tooltip("The desired cell aspect ratio (width / height). For example, 1:1 is a square, 2:1 is twice as wide as tall")]
        public Vector2 cellRatio = Vector2.one;

        [Tooltip("How the grid expands: ExpandRows minimizes columns, ExpandColumns minimizes rows")]
        public ExpandMode expandMode = ExpandMode.ExpandRows;

        [Tooltip("Direction the grid flows")]
        public FlowMode flowMode = FlowMode.Horizontally;
        #endregion

        private int m_actualMinColumnCount;
        private int m_actualMinRowCount;
        private int m_currentColumnCount;
        private int m_currentRowCount;

        protected override void OnValidate()
        {
            GetCellSize(minColumnCount, minRowCount, out float initialCellWidth, out float initialCellHeight);

            // Determine how many rows can fit without changing cell height
            int maxRowsThatFit = Mathf.FloorToInt((rectTransform.rect.size.y - padding.vertical + spacing.y) / (initialCellHeight + spacing.y));
            m_actualMinRowCount = Mathf.Max(maxRowsThatFit, minRowCount);
            m_currentRowCount = m_actualMinRowCount;

            // Determine how many columns can fit without changing cell width
            int maxColumnsThatFit = Mathf.FloorToInt((rectTransform.rect.size.x - padding.horizontal + spacing.x) / (initialCellWidth + spacing.x));
            m_actualMinColumnCount = Mathf.Max(maxColumnsThatFit, minColumnCount);
            m_currentColumnCount = m_actualMinColumnCount;

            UpdateConstraint();
            m_CellSize.x = initialCellWidth;
            m_CellSize.y = initialCellHeight;

            UpdateGridDimensions();
            base.OnValidate();
        }

        public override void SetLayoutHorizontal()
        {
            UpdateGridDimensions();
            base.SetLayoutHorizontal();
        }

        public override void SetLayoutVertical()
        {
            UpdateGridDimensions();
            base.SetLayoutVertical();
        }

        private void UpdateGridDimensions()
        {
            m_currentColumnCount = m_actualMinColumnCount;
            m_currentRowCount = m_actualMinRowCount;

            if (autoResizeGrid)
            {
                int childCount = transform.childCount;

                if (expandMode == ExpandMode.ExpandRows)
                {
                    // Expand rows: minimize columns, expand rows
                    int minRowsNeeded = Mathf.CeilToInt((float)childCount / m_actualMinColumnCount);
                    int targetRowCount = Mathf.Max(minRowsNeeded, m_actualMinRowCount);

                    int bestColumnCount = m_actualMinColumnCount;

                    for (int testRowCount = m_actualMinRowCount; testRowCount <= targetRowCount; testRowCount++)
                    {
                        GetCellSize(m_actualMinColumnCount, testRowCount, out float cellWidth, out _);
                        int maxColumnsThatFit = Mathf.FloorToInt((rectTransform.rect.size.x - padding.horizontal + spacing.x) / (cellWidth + spacing.x));
                        int actualColumns = Mathf.Max(maxColumnsThatFit, m_actualMinColumnCount);

                        int capacityNeeded = Mathf.CeilToInt((float)childCount / actualColumns);
                        if (capacityNeeded <= testRowCount)
                        {
                            bestColumnCount = actualColumns;
                            m_currentRowCount = testRowCount;
                            break;
                        }
                    }

                    m_currentColumnCount = bestColumnCount;
                }
                else // ExpandColumns
                {
                    // Expand columns: minimize rows, expand columns
                    int minColumnsNeeded = Mathf.CeilToInt((float)childCount / m_actualMinRowCount);
                    int targetColumnCount = Mathf.Max(minColumnsNeeded, m_actualMinColumnCount);

                    int bestRowCount = m_actualMinRowCount;

                    for (int testColumnCount = m_actualMinColumnCount; testColumnCount <= targetColumnCount; testColumnCount++)
                    {
                        GetCellSize(testColumnCount, m_actualMinRowCount, out _, out float cellHeight);
                        int maxRowsThatFit = Mathf.FloorToInt((rectTransform.rect.size.y - padding.vertical + spacing.y) / (cellHeight + spacing.y));
                        int actualRows = Mathf.Max(maxRowsThatFit, m_actualMinRowCount);

                        int capacityNeeded = Mathf.CeilToInt((float)childCount / testColumnCount);
                        if (capacityNeeded <= actualRows)
                        {
                            m_currentColumnCount = testColumnCount;
                            bestRowCount = actualRows;
                            break;
                        }
                    }

                    m_currentRowCount = bestRowCount;
                }
            }

            UpdateConstraint();
            GetCellSize(m_currentColumnCount, m_currentRowCount, out m_CellSize.x, out m_CellSize.y);
            SetDirty();
        }

        private void UpdateConstraint()
        {
            if (flowMode == FlowMode.Horizontally)
            {
                constraint = Constraint.FixedColumnCount;
                constraintCount = m_currentColumnCount;
                startAxis = Axis.Horizontal;
            }
            else
            {
                constraint = Constraint.FixedRowCount;
                constraintCount = m_currentRowCount;
                startAxis = Axis.Vertical;
            }
        }

        private void GetCellSize(int columnCount, int rowCount, out float cellWidth, out float cellHeight)
        {
            cellWidth = (rectTransform.rect.size.x - padding.horizontal - spacing.x * (columnCount - 1)) / columnCount;
            cellHeight = (rectTransform.rect.size.y - padding.vertical - spacing.y * (rowCount - 1)) / rowCount;

            if (maintainCellRatio && cellRatio != Vector2.zero)
            {
                // Calculate the desired ratio (width / height)
                float desiredRatio = cellRatio.x / cellRatio.y;
                float currentRatio = cellWidth / cellHeight;

                if (currentRatio > desiredRatio)
                {
                    // Current cell is too wide, constrain by height
                    cellWidth = cellHeight * desiredRatio;
                }
                else
                {
                    // Current cell is too tall, constrain by width
                    cellHeight = cellWidth / desiredRatio;
                }
            }
        }
    }
}
