# 📈 TRD2022 
### Robot that operates cryptocurrencies on Binance. Recommends, buys and sells assets according to the code validations and engines.
<hr>

### 🚧 Next features:
- [x] File to recover open positions
- [x] Engines switches in appsettings.json
- [ ] Queue/Topic communication (SQS and Kafka)
- [x] Dummy version (application only simulate the trades)

<hr>

### ❓ How it works
TRD2022 has three main engines:
- <b>Recommendation</b>
  <br> Uses three types of calculation, that can be turned off in appsettings.json, with candlesticks combined with a moving average validation. It can recommend N number of assets and it's up to the buy engine to decide which one will enter and balance the three types of recommendations. If an asset that have been sold appear again on the recommendation engine, the code has a validation to only proceed with the recommendation of the asset if its current price is X% higher than when it was sold, with the intention to avoid buying an asset on bear market.
- <b>Buy</b>
  <br> Responsible to receive the recommendations and try to buy the assets in a good time. It has validations to make attempts to buy the asset in an up valorization, if exceeds the maximum number of attempts without finding a good spot to buy, it will try the next recommended asset. The validations and loops try to prevent purchases of assets that started a bear market.
- <b>Sell</b>
    <br> Similar with the 'buy' engine concept, attempts to sell the asset in a good time. The TRD has a limiter on the loss of a asset (that can be changed in the appsettiongs.json), if hit that limiter, it calls the sell engine who is responsible to make validations and sell the asset in a little price rise in N attempts, if exceeds this attempts without a up valorization, sells the asset.
    
Managing when and how to call these engines is the main feature in TRD, besides every method on all services, the most responsible for that is the ManagePosition() on <a href="https://github.com/anddMF/TRD2022/blob/93f9912d9dad86ddc9b148217f974ca2a1970862/Trade02/Business/services/PortfolioService.cs#L53">PortfolioService.cs<a/>. It has the following tasks inside it:
  - Manages the percentage to leave a position based on the overall profit;
  - Checks on the open positions if it needs to call the sell engine or keep them open;
  - Validates the cap on open positions per type of recommendations and balance the new assets based on it;
  - If it is possible to open new positions, and have receive it from the recommendation engine, iterates throught the recommendations calling the buy engine;

  The one that dictates the pace of TRD and manage the callings and objects is the <a href="https://github.com/anddMF/TRD2022/blob/main/Trade02/Worker.cs">Worker.cs</a> itself. It is responsible for calling the recommendation engine (when available), sending and capturing the information from ManagePosition() and validating the number of open positions, all of this happens every 20 seconds inside a loop and its only finished when it hits the maximum profit of the day.
  
 Finally, TRD has two file systems to report operations and recover positions. The positions.csv (saved on folder 'WALLET' and administrated on <a href="https://github.com/anddMF/TRD2022/blob/main/Trade02/Infra/Cross/WalletManagement.cs">WalletManagement.cs</a>) is a single file that saves the current open positions from the TRD, so in a scenario where TRD is shutted down, when it is rebooted it will go back to when he was before the shut down. The other file system is responsible to make a report on all the movements that TRD did on the day, it is managed on <a href="https://github.com/anddMF/TRD2022/blob/main/Trade02/Infra/Cross/ReportLog.cs">ReportLog.cs</a> and saves a file per day on folder 'REPORTS' with all the relevant data from the operation, including the type of recommendation on the asset.
  
  This is a simple high level explanation of TRD, just to give it a north for searching things on the code. For example, I didn't talked about the balance the code does on the amount of USDT it uses on every operation and how the configuration of the robot on the appsettings.json can change the whole application.
  
  ⚠️ TRD is a working in progress and this is already the third version of the robot in four months, everything can be different in the next weeks but, for now, this version is the most solid in profits so far. ⚠️
  
 
