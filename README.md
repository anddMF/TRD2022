# TRD2022 📈
### Robot that operates cryptocurrencies on Binance. Recommends, buys and sells assets according to the code validations and engines.
<hr>

### 🚧 Next features:
- [x] File to recover open positions
- [x] Engines switches in appsettings.json
- [ ] Queue communication (SQS and Kafka)

<hr>

### ❓ How it works
TRD2022 has three main engines:
- <b>Recommendation</b>
  <br> Uses three types of calculation, that can be turned off in appsettings.json, with candlesticks combined with a moving average validation. It can recommend N number of assets and it's up to the buy engine to decide which one will enter and balance the three types of recommendations. 
- <b>Buy</b>
  <br> Responsible to receive the recommendations and try to buy the assets in a good time. It has validations to make attempts to buy the asset in an up valorization, if exceeds the maximum number of attempts without finding a good spot to buy, it will try the next recommended asset. The validations and loops try to prevent purchases of assets that started a bear market.
- <b>Sell</b>
    <br> Similar with the 'buy' engine concept, attempts to sell the asset in a good time. The TRD has a limiter on the loss of a asset (that can be changed in the appsettiongs.json), if hit that limiter, it calls the sell engine who is responsible to make validations and sell the asset in a little price rise in N attempts, if exceeds this attempts without a up valorization, sells the asset.
