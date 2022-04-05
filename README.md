# TRD2022 📈
### Robot that operates cryptocurrencies on Binance. Reccomends, buys and sells assets according to the code validations and engines.
<hr>

### 🚧 Next features:
- [x] File to recover open positions
- [x] Engines switches in appsettings.json
- [ ] Queue communication (SQS and Kafka)

<hr>

### ❓ How it works
TRD2022 has three main engines:
- <b>Recommendation</b>
  - uses three types of calculation, that can be turned off in appsettings.json, with candlesticks combined with a moving average validation. It can recommend N number of assets and it's up to the buy engine to decide which one will enter and balance the three types of recommendations. 
- <b>Buy</b>
  - responsible to receive the recommendations and try to buy the assets in a good time. It has validations to make attempts to buy the asset in an up valorization, if exceeds the maximum number of attempts without finding a good spot to buy, it will try the next recommended asset. The validations and loops try to prevent purchases of assets that started a bear market.
- Sell
 
