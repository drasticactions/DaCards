using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Classic.Avalonia.Theme;
using DaCards;

namespace DaCardsAV;

public partial class MainWindow : ClassicWindow
{
    private SolitareGameBoard? _solitaireBoard;
    private SpiderSolitareGameBoard? _spiderBoard;
    private FreeCellGameBoard? _freeCellBoard;
    private GameType _currentGameType = GameType.None;
    private SpiderDifficulty _spiderDifficulty = SpiderDifficulty.OneSuit;
    private int _solitaireDrawCount = 1;

    private enum GameType { None, Solitaire, Spider, FreeCell }

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnSolitaireDrawOne(object? sender, RoutedEventArgs e)
    {
        StartSolitaireGame(1);
    }

    private void OnSolitaireDrawThree(object? sender, RoutedEventArgs e)
    {
        StartSolitaireGame(3);
    }

    private void StartSolitaireGame(int drawCount)
    {
        _currentGameType = GameType.Solitaire;
        _solitaireDrawCount = drawCount;
        Title = $"Solitaire - Draw {drawCount}";
        _solitaireBoard ??= new SolitareGameBoard();
        GameContainer.Content = _solitaireBoard;
        _solitaireBoard.NewGame(drawCount);
    }

    private void OnFreeCell(object? sender, RoutedEventArgs e)
    {
        _currentGameType = GameType.FreeCell;
        Title = "FreeCell";
        _freeCellBoard ??= new FreeCellGameBoard();
        GameContainer.Content = _freeCellBoard;
        _freeCellBoard.NewGame();
    }

    private void OnSpiderOneSuit(object? sender, RoutedEventArgs e)
    {
        StartSpiderGame(SpiderDifficulty.OneSuit);
    }

    private void OnSpiderTwoSuits(object? sender, RoutedEventArgs e)
    {
        StartSpiderGame(SpiderDifficulty.TwoSuits);
    }

    private void OnSpiderFourSuits(object? sender, RoutedEventArgs e)
    {
        StartSpiderGame(SpiderDifficulty.FourSuits);
    }

    private void StartSpiderGame(SpiderDifficulty difficulty)
    {
        _currentGameType = GameType.Spider;
        _spiderDifficulty = difficulty;

        var difficultyName = difficulty switch
        {
            SpiderDifficulty.OneSuit => "1 Suit",
            SpiderDifficulty.TwoSuits => "2 Suits",
            _ => "4 Suits"
        };
        Title = $"Spider Solitaire - {difficultyName}";

        _spiderBoard ??= new SpiderSolitareGameBoard();
        GameContainer.Content = _spiderBoard;
        _spiderBoard.NewGame(difficulty);
    }

    private void OnNewGame(object? sender, RoutedEventArgs e)
    {
        switch (_currentGameType)
        {
            case GameType.Solitaire:
                _solitaireBoard?.NewGame(_solitaireDrawCount);
                break;
            case GameType.FreeCell:
                _freeCellBoard?.NewGame();
                break;
            case GameType.Spider:
                _spiderBoard?.NewGame(_spiderDifficulty);
                break;
        }
    }

    private void OnUndo(object? sender, RoutedEventArgs e)
    {
        switch (_currentGameType)
        {
            case GameType.Solitaire:
                _solitaireBoard?.Undo();
                break;
            case GameType.FreeCell:
                _freeCellBoard?.Undo();
                break;
            case GameType.Spider:
                _spiderBoard?.Undo();
                break;
        }
    }

    private void OnRedo(object? sender, RoutedEventArgs e)
    {
        switch (_currentGameType)
        {
            case GameType.Solitaire:
                _solitaireBoard?.Redo();
                break;
            case GameType.FreeCell:
                _freeCellBoard?.Redo();
                break;
            case GameType.Spider:
                _spiderBoard?.Redo();
                break;
        }
    }

    private void OnExit(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnPlayWinAnimation(object? sender, RoutedEventArgs e)
    {
        switch (_currentGameType)
        {
            case GameType.Solitaire:
                _solitaireBoard?.TriggerWinAnimation();
                break;
            case GameType.FreeCell:
                _freeCellBoard?.TriggerWinAnimation();
                break;
            case GameType.Spider:
                _spiderBoard?.TriggerWinAnimation();
                break;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.Z:
                    OnUndo(null, e);
                    e.Handled = true;
                    break;
                case Key.Y:
                    OnRedo(null, e);
                    e.Handled = true;
                    break;
                case Key.N:
                    OnNewGame(null, e);
                    e.Handled = true;
                    break;
            }
        }
    }

    private void SetCardBack(int deckNumber)
    {
        _solitaireBoard?.SetCardBack(deckNumber);
        _freeCellBoard?.SetCardBack(deckNumber);
        _spiderBoard?.SetCardBack(deckNumber);
    }

    private void OnCardBack1(object? sender, RoutedEventArgs e) => SetCardBack(1);
    private void OnCardBack2(object? sender, RoutedEventArgs e) => SetCardBack(2);
    private void OnCardBack3(object? sender, RoutedEventArgs e) => SetCardBack(3);
    private void OnCardBack4(object? sender, RoutedEventArgs e) => SetCardBack(4);
    private void OnCardBack5(object? sender, RoutedEventArgs e) => SetCardBack(5);
    private void OnCardBack6(object? sender, RoutedEventArgs e) => SetCardBack(6);
    private void OnCardBack7(object? sender, RoutedEventArgs e) => SetCardBack(7);
    private void OnCardBack8(object? sender, RoutedEventArgs e) => SetCardBack(8);
    private void OnCardBack9(object? sender, RoutedEventArgs e) => SetCardBack(9);
    private void OnCardBack10(object? sender, RoutedEventArgs e) => SetCardBack(10);
    private void OnCardBack11(object? sender, RoutedEventArgs e) => SetCardBack(11);
    private void OnCardBack12(object? sender, RoutedEventArgs e) => SetCardBack(12);

    private void OnAutoMoveToggle(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        GameSettings.AutoMoveEnabled = !GameSettings.AutoMoveEnabled;
        
        // Update the checkbox icon
        if (AutoMoveMenuItem.Icon is CheckBox checkBox)
        {
            checkBox.IsChecked = GameSettings.AutoMoveEnabled;
        }
    }
}
