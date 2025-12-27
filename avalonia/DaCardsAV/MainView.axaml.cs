using Avalonia.Controls;
using Avalonia.Interactivity;
using DaCards;

namespace DaCardsAV;

public partial class MainView : UserControl
{
    private SolitareGameBoard? _solitaireBoard;
    private FreeCellGameBoard? _freeCellBoard;
    private SpiderSolitareGameBoard? _spiderBoard;

    private GameType _currentGameType = GameType.None;
    private int _solitaireDrawCount = 1;
    private SpiderDifficulty _spiderDifficulty = SpiderDifficulty.OneSuit;

    private enum GameType { None, Solitaire, FreeCell, Spider }

    public MainView()
    {
        InitializeComponent();
    }

    private void ShowMenu()
    {
        _currentGameType = GameType.None;
        GameToolbar.IsVisible = false;
        ContentArea.Content = MainMenu;
    }

    private void ShowGame(Control gameBoard, string title)
    {
        GameToolbar.IsVisible = true;
        GameTitle.Text = title;
        ContentArea.Content = gameBoard;
    }

    private void OnBackToMenu(object? sender, RoutedEventArgs e)
    {
        ShowMenu();
    }

    private void OnSolitaireDrawOne(object? sender, RoutedEventArgs e)
    {
        StartSolitaire(1);
    }

    private void OnSolitaireDrawThree(object? sender, RoutedEventArgs e)
    {
        StartSolitaire(3);
    }

    private void StartSolitaire(int drawCount)
    {
        _currentGameType = GameType.Solitaire;
        _solitaireDrawCount = drawCount;
        _solitaireBoard ??= new SolitareGameBoard();
        _solitaireBoard.NewGame(drawCount);
        ShowGame(_solitaireBoard, $"Solitaire - Draw {drawCount}");
    }

    private void OnFreeCell(object? sender, RoutedEventArgs e)
    {
        _currentGameType = GameType.FreeCell;
        _freeCellBoard ??= new FreeCellGameBoard();
        _freeCellBoard.NewGame();
        ShowGame(_freeCellBoard, "FreeCell");
    }

    private void OnSpiderOneSuit(object? sender, RoutedEventArgs e)
    {
        StartSpider(SpiderDifficulty.OneSuit, "1 Suit");
    }

    private void OnSpiderTwoSuits(object? sender, RoutedEventArgs e)
    {
        StartSpider(SpiderDifficulty.TwoSuits, "2 Suits");
    }

    private void OnSpiderFourSuits(object? sender, RoutedEventArgs e)
    {
        StartSpider(SpiderDifficulty.FourSuits, "4 Suits");
    }

    private void StartSpider(SpiderDifficulty difficulty, string difficultyName)
    {
        _currentGameType = GameType.Spider;
        _spiderDifficulty = difficulty;
        _spiderBoard ??= new SpiderSolitareGameBoard();
        _spiderBoard.NewGame(difficulty);
        ShowGame(_spiderBoard, $"Spider - {difficultyName}");
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
}
